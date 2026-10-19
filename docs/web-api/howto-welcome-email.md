# Send a welcome email

## Introduction

At the end of this guide every new user who calls `POST /register` gets a welcome email with a link to confirm their address, and the confirmation and password reset mails go through the same SMTP sender. In development you read them all in smtp4dev.

Before you start, you need Identity API endpoints from [Add login](/web-api/howto-login) and the `IEmailSender`, `EmailOptions`, and `SmtpEmailSender` classes from [Sending email](/web-api/email).

## 1. Write the Identity email sender

```csharp [Services/IdentityEmailSender.cs]
using System.Net;
using Microsoft.AspNetCore.Identity;

namespace TodoApi.Services;

public class IdentityEmailSender(IEmailSender sender) : IEmailSender<IdentityUser>
{
    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink) =>
        sender.SendAsync(email, "Welcome to TodoApi",
            $"""
            Thanks for signing up.

            Confirm your email address by opening this link:
            {WebUtility.HtmlDecode(confirmationLink)}
            """);

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink) =>
        sender.SendAsync(email, "Reset your password",
            $"Open this link to reset your password: {WebUtility.HtmlDecode(resetLink)}");

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode) =>
        sender.SendAsync(email, "Your password reset code",
            $"""
            Someone asked to reset the password for this account.

            Your reset code is:
            {WebUtility.HtmlDecode(resetCode)}

            It expires in 2 hours. If you did not ask for this, ignore this email.
            """);
}
```

`IEmailSender<IdentityUser>` is the interface `MapIdentityApi` calls whenever it needs to send mail. It lives in `Microsoft.AspNetCore.Identity` and is generic, so it does not clash with your own `IEmailSender` from [Sending email](/web-api/email). This class only writes the text and hands it to your SMTP sender.

Identity passes the link already HTML-encoded, so `&` arrives as `&amp;`. The messages here are plain text, so `WebUtility.HtmlDecode` turns it back into a link that works when clicked. If you send HTML mail instead, use the value as it is.

## 2. Register it

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.Identity;
using TodoApi.Services;

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddTransient<IEmailSender<IdentityUser>, IdentityEmailSender>();
```

Without this line Identity uses a built-in sender that does nothing, so registration works but no mail goes out. The first two lines are the ones you already have from [Sending email](/web-api/email).

## 3. Require a confirmed email to log in

```csharp [Program.cs (excerpt)]
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();
```

This step is optional. With it, `/login` refuses users who have not opened the link yet, so nobody can sign up with someone else's address. Leave it off if you only want a friendly welcome message.

## 4. Start smtp4dev

```bash
docker run --rm -p 3000:80 -p 2525:25 rnwood/smtp4dev
```

smtp4dev catches every message and shows it at `http://localhost:3000`. Port `2525` matches the `Email` section in `appsettings.Development.json` from [Sending email](/web-api/email). Without Docker, run `dotnet tool install -g Rnwood.Smtp4dev` and then `smtp4dev`.

## 5. Try it

```http [TodoApi.http]
### Register
POST {{TodoApi_HostAddress}}/register
Content-Type: application/json

{ "email": "dave@example.com", "password": "Passw0rd!" }

### Log in before confirming
POST {{TodoApi_HostAddress}}/login
Content-Type: application/json

{ "email": "dave@example.com", "password": "Passw0rd!" }
```

`/register` returns `200 OK`, and smtp4dev shows a message titled "Welcome to TodoApi" with a link like `http://localhost:5000/confirmEmail?userId=...&code=...`. With step 3 in place, the login request returns `401` with `"detail": "NotAllowed"`.

Open the link in a browser. The page says `Thank you for confirming your email.` Send the login request again and you get an `accessToken`.

If smtp4dev is not running, `/register` returns `500`. The user row is already saved by then, so the address is taken. The client can ask for a new link with `POST /resendConfirmationEmail` and `{ "email": "dave@example.com" }`.

::: tip
The request waits for the SMTP server. To keep sign-up fast when mail is slow, queue the message and send it from a [background task](/web-api/background-tasks).
:::

## Learn more

- [Sending email](/web-api/email) for the SMTP sender and user secrets
- [Reset a forgotten password](/web-api/howto-password-reset)
- [Add login](/web-api/howto-login)
- [Account confirmation and password recovery in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/accconfirm)
