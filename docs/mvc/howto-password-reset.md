# Reset a forgotten password

## Introduction

At the end of this guide, a BookShelf user who forgot their password can request a reset link by email, open it, and choose a new password. Before you start, you need login from [Add login](/mvc/howto-login) and the email classes from [Sending email](/mvc/email). The Identity UI already contains the pages. Your job is to give them a way to send email.

## 1. Know the pages you already have

The Identity UI package includes four pages for this flow.

- `/Identity/Account/ForgotPassword` asks for an email address.
- `/Identity/Account/ForgotPasswordConfirmation` says "check your email".
- `/Identity/Account/ResetPassword?code=...` lets the user set a new password.
- `/Identity/Account/ResetPasswordConfirmation` says the password changed.

The login page already links to ForgotPassword with "Forgot your password?". Nothing needs scaffolding.

## 2. Plug in a real email sender

```csharp [Services/IdentityEmailSender.cs]
namespace BookShelf.Services;

public class IdentityEmailSender(IEmailSender sender)
    : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage) =>
        sender.SendAsync(email, subject, htmlMessage);
}
```

```csharp [Program.cs (excerpt)]
using BookShelf.Services;

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddTransient<
    Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, IdentityEmailSender>();
```

Without this, Identity uses a sender that does nothing. The ForgotPassword page still says "Please check your email", but no email ever leaves the app, and nothing is logged. This is the most common reason password reset "does nothing".

The adapter is the same one [Send a welcome email](/mvc/howto-welcome-email) uses. If you already added it there, skip this step.

## 3. Require confirmed accounts

```csharp [Program.cs (excerpt)]
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<AppDbContext>();
```

ForgotPassword only sends a link when the email belongs to a user with a confirmed address. For any other address it shows the same "check your email" page and sends nothing. That way an attacker cannot use the form to find out which emails have accounts, and cannot send reset links to an address nobody proved they own.

`RequireConfirmedAccount = true` makes every user confirm their email when they register, so the reset form works for all of them.

## 4. Set how long a link stays valid

```csharp [Program.cs (excerpt)]
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(2));
```

The `code` in the reset link is a token, a signed value that Identity checks when the user submits the new password. The default lifetime is one day. Two hours is a common choice for reset links. This setting also applies to email confirmation tokens, because both use the same token provider.

A token stops working as soon as the password changes, so each link can be used only once.

## 5. Use HTML email for the link

Identity builds the email body as HTML with an `<a href="...">` link. The `SmtpEmailSender` from [Sending email](/mvc/email) sends plain text, so the user sees the raw tag. Change one line in `Services/SmtpEmailSender.cs`.

```csharp [Services/SmtpEmailSender.cs (excerpt)]
message.Body = new TextPart("html") { Text = body };
```

If other code sends plain text through the same sender, it still displays fine as HTML, apart from line breaks.

## 6. Try it

Start smtp4dev as shown in [Send a welcome email](/mvc/howto-welcome-email), so you can read the mails locally. Run the app and register and confirm `reader@example.com` if you have not yet.

1. Open `https://localhost:<port>/Identity/Account/Login` and click "Forgot your password?".
2. Enter `reader@example.com` and submit. You see "Forgot password confirmation".
3. In the smtp4dev inbox, open the "Reset Password" email and click the link.
4. On the Reset password page, enter the email and a new password twice. You see "Your password has been reset".
5. Log in with the new password.

Click the same link again and submit. Identity shows "Invalid token.", which confirms each link only works once.

## Learn more

- [Sending email](/mvc/email)
- [Authentication](/mvc/authentication)
- [Send a welcome email](/mvc/howto-welcome-email)
- [Account confirmation and password recovery in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/accconfirm)
