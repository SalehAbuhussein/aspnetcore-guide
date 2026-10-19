# Send a welcome email

## Introduction

At the end of this guide, every new BookShelf user gets a "Welcome to BookShelf" email right after registering, and Identity's own confirmation and password reset emails go out through the same SMTP sender. Before you start, you need login from [Add login](/mvc/howto-login) and the `IEmailSender`, `SmtpOptions`, and `SmtpEmailSender` classes from [Sending email](/mvc/email).

## 1. Pick the hook

Identity gives you two places to send mail from.

- Scaffold the Register page with the code generator and add a send call to its `OnPostAsync`. This copies a few hundred lines of Identity code into your project, which you then maintain.
- Replace `UserManager<IdentityUser>` with a subclass and override `CreateAsync`. The built-in Register page calls `CreateAsync(user, password)`, so your override runs without any scaffolding.

This guide uses the second option. It is one small class and keeps working when you update the Identity packages.

## 2. Connect Identity's sender to yours

```csharp [Services/IdentityEmailSender.cs]
namespace BookShelf.Services;

public class IdentityEmailSender(IEmailSender sender)
    : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage) =>
        sender.SendAsync(email, subject, htmlMessage);
}
```

The Identity pages send confirmation and reset emails through `Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`. By default that sender does nothing. This adapter forwards every Identity email to your own `IEmailSender`, the one with `SendAsync`. The full namespace keeps the two interfaces apart.

Identity passes an HTML body, while `SmtpEmailSender` sends plain text. The emails still arrive, but the link shows as raw HTML. Change `new TextPart("plain")` to `new TextPart("html")` in `SmtpEmailSender` if you want clickable links.

## 3. Send the welcome email after the user is created

```csharp [Services/WelcomeUserManager.cs]
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BookShelf.Services;

public class WelcomeUserManager(
    IUserStore<IdentityUser> store,
    IOptions<IdentityOptions> optionsAccessor,
    IPasswordHasher<IdentityUser> passwordHasher,
    IEnumerable<IUserValidator<IdentityUser>> userValidators,
    IEnumerable<IPasswordValidator<IdentityUser>> passwordValidators,
    ILookupNormalizer keyNormalizer,
    IdentityErrorDescriber errors,
    IServiceProvider services,
    ILogger<UserManager<IdentityUser>> logger,
    IEmailSender email)
    : UserManager<IdentityUser>(store, optionsAccessor, passwordHasher, userValidators,
        passwordValidators, keyNormalizer, errors, services, logger)
{
    public override async Task<IdentityResult> CreateAsync(IdentityUser user, string password)
    {
        var result = await base.CreateAsync(user, password);

        if (result.Succeeded && user.Email is not null)
        {
            try
            {
                await email.SendAsync(
                    user.Email,
                    "Welcome to BookShelf",
                    "Thanks for signing up. Start by adding the first book to your shelf.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Welcome email to {Email} failed", user.Email);
            }
        }

        return result;
    }
}
```

The first nine constructor parameters are the ones `UserManager` itself needs, passed straight to the base class. The last one is your sender. The email goes out only when the user was saved.

The `try`/`catch` matters. Without it, a down SMTP server makes registration show an error page even though the account exists. `Logger` is a property of `UserManager`.

## 4. Register the services

```csharp [Program.cs (excerpt)]
using BookShelf.Services;

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddTransient<
    Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, IdentityEmailSender>();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddUserManager<WelcomeUserManager>()
    .AddEntityFrameworkStores<AppDbContext>();
```

`AddUserManager<WelcomeUserManager>` makes Identity hand out your class wherever it asks for `UserManager<IdentityUser>`. Once a real sender is registered, the Register page stops showing the "click here to confirm" shortcut and sends the confirmation email instead.

Anything else that calls `CreateAsync`, such as the admin seeding in [Add an admin role](/mvc/howto-admin-role), also sends a welcome email.

## 5. Catch the mail with smtp4dev

```bash
dotnet tool install -g Rnwood.Smtp4dev
smtp4dev --smtpport=2525
```

smtp4dev is a fake mail server. It accepts every message and shows it in a web inbox at `http://localhost:5000`. Point BookShelf at it in development.

```json [appsettings.Development.json (excerpt)]
"Smtp": {
  "Host": "localhost",
  "Port": 2525,
  "FromAddress": "bookshelf@example.com"
}
```

Leave `UserName` empty, because smtp4dev does not ask for a login. Port 2525 avoids clashing with anything already using port 25. If BookShelf also runs on port 5000, start smtp4dev with `--urls=http://localhost:5050`.

## 6. Try it

Run `dotnet run`, open `https://localhost:<port>/Identity/Account/Register`, and register `reader@example.com`. Open the smtp4dev inbox. You see two messages for that address: "Welcome to BookShelf" and "Confirm your email". Click the link in the confirmation email, then log in.

Stop smtp4dev and register another user. Registration still succeeds, and the console shows `Welcome email to ... failed`.

## Learn more

- [Sending email](/mvc/email)
- [Background tasks](/mvc/background-tasks), to send the email off the request
- [Reset a forgotten password](/mvc/howto-password-reset)
- [Account confirmation and password recovery in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/accconfirm)
