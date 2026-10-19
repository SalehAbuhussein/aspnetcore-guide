# Sending email

## Introduction

This page sends an email when a todo is marked done. You will define your own `IEmailSender` interface, implement it with MailKit over SMTP, keep the settings in configuration, and catch every message on your machine with a local test server.

<ApiStyle />

## Install MailKit

```bash
dotnet add package MailKit
```

.NET has an old `System.Net.Mail.SmtpClient` class, but Microsoft recommends against it for new code. MailKit is the library most .NET apps use for SMTP. SMTP is the protocol mail servers use to accept and pass on email.

## The interface

```csharp [Services/IEmailSender.cs]
namespace TodoApi.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
```

Your endpoints depend on this interface, not on MailKit. In tests you can swap in a fake that records messages, and if you move to an email API such as SendGrid later, only one class changes. See [Dependency injection](/web-api/dependency-injection).

## The settings

```csharp [Services/EmailOptions.cs]
namespace TodoApi.Services;

public class EmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "";
    public string NotifyTo { get; set; } = "";
}
```

```json [appsettings.Development.json]
{
  "Email": {
    "Host": "localhost",
    "Port": 2525,
    "From": "todos@example.com",
    "NotifyTo": "me@example.com"
  }
}
```

This is the options pattern. You describe a settings section as a class, and ASP.NET Core fills it from configuration. `NotifyTo` is the address that hears about completed todos. The [Configuration](/web-api/configuration) page explains where values come from.

## The MailKit sender

```csharp [Services/SmtpEmailSender.cs]
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TodoApi.Services;

public class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.Auto, cancellationToken);

        if (!string.IsNullOrEmpty(settings.UserName))
        {
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? "", cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
```

`MimeMessage` comes from MimeKit, which MailKit installs for you. It builds the email itself. `SmtpClient` here is MailKit's class, from the `MailKit.Net.Smtp` namespace, not the old one in `System.Net.Mail`. `SecureSocketOptions.Auto` picks TLS encryption based on the port and what the server offers. The login step is skipped when no user name is set, which is the case for the local test server.

## Register it

```csharp [Program.cs (excerpt)]
using TodoApi.Services;

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
```

`Configure<EmailOptions>` binds the `Email` section to the class. Any service can then ask for `IOptions<EmailOptions>`.

## Keep the password out of source control

```bash
dotnet user-secrets init
dotnet user-secrets set "Email:UserName" "apikey"
dotnet user-secrets set "Email:Password" "your-smtp-password"
```

User secrets is a settings store that lives in your user profile, outside the project folder, so it never ends up in Git. ASP.NET Core loads it in Development and merges it over `appsettings.json`. The `:` in the key means "inside the `Email` section". In production, set the same values as environment variables named `Email__Password`.

## Catch email locally with smtp4dev

```bash
docker run --rm -p 3000:80 -p 2525:25 rnwood/smtp4dev
```

smtp4dev is a fake SMTP server for development. It accepts every message and shows it in a web page instead of delivering it. Open `http://localhost:3000` to see your inbox. The command maps its SMTP port to `2525`, which matches `appsettings.Development.json`. No Docker? Install it as a .NET tool with `dotnet tool install -g Rnwood.Smtp4dev` and run `smtp4dev`, then check the ports it prints.

::: tip
Use a fake SMTP server for all local work. Real addresses in a test database are easy to email by mistake.
:::

## Send when a todo is completed

<div class="only-minimal">

Change `Update` in `Endpoints/TodoEndpoints.cs`:

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
// at the top of the file
using Microsoft.Extensions.Options;
using TodoApi.Services;

// in the TodoEndpoints class
static async Task<Results<NoContent, NotFound>> Update(
    int id, UpdateTodo input, AppDbContext db, IEmailSender email, IOptions<EmailOptions> options)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return TypedResults.NotFound();
    }

    var justCompleted = !todo.IsDone && input.IsDone;

    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
    await db.SaveChangesAsync();

    if (justCompleted)
    {
        await email.SendAsync(options.Value.NotifyTo, "Todo completed", $"\"{todo.Title}\" is done.");
    }

    return TypedResults.NoContent();
}
```

</div>

<div class="only-controllers">

Add the email services to the constructor of `Controllers/TodosController.cs` and change `Update`:

```csharp [Controllers/TodosController.cs (excerpt)]
// at the top of the file
using Microsoft.Extensions.Options;
using TodoApi.Services;

// add the services to the primary constructor, keeping any you already have
public class TodosController(
    AppDbContext db,
    IEmailSender email,
    IOptions<EmailOptions> options) : ControllerBase

// in the TodosController class
[HttpPut("{id:int}")]
public async Task<IActionResult> Update(int id, UpdateTodo input)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return NotFound();
    }

    var justCompleted = !todo.IsDone && input.IsDone;

    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
    await db.SaveChangesAsync();

    if (justCompleted)
    {
        await email.SendAsync(options.Value.NotifyTo, "Todo completed", $"\"{todo.Title}\" is done.");
    }

    return NoContent();
}
```

</div>

`justCompleted` is true only when the todo goes from open to done, so saving an already finished todo does not send a second email. The email goes out after `SaveChangesAsync`, so you never announce a change that failed to save.

Send the `PUT` request from `TodoApi.http` with `"isDone": true` and the message appears in smtp4dev.

::: warning
The request now waits for the SMTP server. If it is slow or down, the client waits too or gets an error, even though the todo was saved. [Background tasks](/web-api/background-tasks) moves the send off the request.
:::

## Learn more

- [Options pattern in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options)
- [Safe storage of app secrets in development](https://learn.microsoft.com/aspnet/core/security/app-secrets)
