# Sending email

## Introduction

This page sends an email each time someone adds a book. You will define a small `IEmailSender` interface, implement it with MailKit over SMTP, load the SMTP settings with the options pattern, and use a fake sender while you develop.

## The interface

```csharp [Services/IEmailSender.cs]
namespace BookShelf.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}
```

Your controllers depend on this interface, not on MailKit. You can then swap the real sender for a logging one in development or a fake one in tests. See [Dependency injection](/mvc/dependency-injection).

ASP.NET Core Identity has its own `IEmailSender` in `Microsoft.AspNetCore.Identity.UI.Services`, with a `SendEmailAsync` method. If a file uses both namespaces, refer to yours as `BookShelf.Services.IEmailSender`.

## SMTP settings

SMTP is the protocol mail servers use to accept email. Put the non-secret settings in `appsettings.json`.

```json [appsettings.json (excerpt)]
"Smtp": {
  "Host": "smtp.example.com",
  "Port": 587,
  "UserName": "bookshelf@example.com",
  "FromAddress": "bookshelf@example.com",
  "FromName": "BookShelf",
  "NotifyAddress": "you@example.com"
}
```

Keep the password out of source control with user secrets.

```bash
dotnet user-secrets init
dotnet user-secrets set "Smtp:Password" "your-smtp-password"
```

User secrets load only in Development. In production, set an environment variable named `Smtp__Password` instead. The [Configuration](/mvc/configuration) page covers both.

Create a class that matches the section.

```csharp [Services/SmtpOptions.cs]
namespace BookShelf.Services;

public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "BookShelf";
    public string NotifyAddress { get; set; } = "";
}
```

This is the options pattern: a plain class filled from a configuration section, which services receive as `IOptions<SmtpOptions>`.

## The MailKit sender

```bash
dotnet add package MailKit
```

MailKit is the most widely used .NET email library. Microsoft recommends it over the old `System.Net.Mail.SmtpClient`.

```csharp [Services/SmtpEmailSender.cs]
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BookShelf.Services;

public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _smtp = options.Value;

    public async Task SendAsync(string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port, SecureSocketOptions.Auto);

        if (!string.IsNullOrEmpty(_smtp.UserName))
        {
            await client.AuthenticateAsync(_smtp.UserName, _smtp.Password ?? "");
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
```

`MimeMessage` is the email itself, from MimeKit, which comes with MailKit. `SmtpClient` here is MailKit's, from `MailKit.Net.Smtp`. `SecureSocketOptions.Auto` uses TLS on port 465 and upgrades to TLS on other ports when the server supports it. The sender skips login when no user name is set, which local test servers need.

## A sender for development

```csharp [Services/LogEmailSender.cs]
namespace BookShelf.Services;

public class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body)
    {
        logger.LogInformation("Email to {To}: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
```

This sender writes the email to the console instead of sending it. Register one or the other based on the environment.

```csharp [Program.cs (excerpt)]
using BookShelf.Services;

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddTransient<IEmailSender, LogEmailSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
}
```

`Configure<SmtpOptions>` binds the `Smtp` section to the class. Each sender is small and holds no state, so transient is a good lifetime.

::: tip
To see real emails in development, run a local test server such as smtp4dev or Papercut SMTP. Each catches every message and shows it in a web page or window. Register `SmtpEmailSender` in Development too, and set `Host` to `localhost` and `Port` to the test server's port (smtp4dev uses 25 by default) in `appsettings.Development.json`. Leave `UserName` empty.
:::

## Sending after a book is added

```csharp [Controllers/BooksController.cs (excerpt)]
using BookShelf.Services;
using Microsoft.Extensions.Options;

public class BooksController(
    AppDbContext db,
    IWebHostEnvironment env,
    IEmailSender email,
    IOptions<SmtpOptions> smtp) : Controller
{
    // POST: /Books/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Create(Book book, IFormFile? cover)
    {
        // The cover checks, the ModelState check, and the
        // SaveCoverAsync call from the File uploads page stay here.

        db.Books.Add(book);
        await db.SaveChangesAsync();

        await email.SendAsync(
            smtp.Value.NotifyAddress,
            $"New book: {book.Title}",
            $"{book.Title} by {book.Author} ({book.Year}) was added to BookShelf.");

        return RedirectToAction(nameof(Index));
    }

    // other actions as before
}
```

The constructor gains two parameters, and the rest of `Create` stays as the [File uploads](/mvc/file-uploads) page left it. The email is sent after `SaveChangesAsync`, so it only goes out once the book is stored. The user waits while the SMTP server answers, which can take a second or more, and an SMTP error would show an error page even though the book was saved. The [Background tasks](/mvc/background-tasks) page moves sending onto a queue to fix both problems.

## Learn more

- [Options pattern in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options)
- [Safe storage of app secrets in development](https://learn.microsoft.com/aspnet/core/security/app-secrets)
