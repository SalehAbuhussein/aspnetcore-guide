# Reset a forgotten password

## Introduction

At the end of this guide a user who forgot their password can ask for a reset code by email and set a new password with it, using the `/forgotPassword` and `/resetPassword` endpoints that Identity already gives you. The code expires after two hours.

Before you start, you need Identity API endpoints from [Add login](/web-api/howto-login) and the `IdentityEmailSender` from [Send a welcome email](/web-api/howto-welcome-email). Reset codes only go to confirmed addresses, so users must have confirmed their email first.

## 1. Check the reset email text

```csharp [Services/IdentityEmailSender.cs (excerpt)]
public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode) =>
    sender.SendAsync(email, "Your password reset code",
        $"""
        Someone asked to reset the password for this account.

        Your reset code is:
        {WebUtility.HtmlDecode(resetCode)}

        It expires in 2 hours. If you did not ask for this, ignore this email.
        """);
```

`/forgotPassword` calls `SendPasswordResetCodeAsync`, not the link method. The code is a long string the user pastes into your app, which then sends it to `/resetPassword`. If your front end has a reset page, you can put the code in a link to it, such as `https://app.example.com/reset?email=...&code=...`, so users do not copy it by hand.

## 2. Set how long codes last

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.Identity;

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(2));
```

Reset codes come from Identity's data protection token provider, which accepts them for one day by default. Two hours is plenty for someone who is waiting for the mail. This setting also applies to the email confirmation links from the welcome email, so keep it long enough for those.

A code works once. After a successful reset Identity changes the user's security stamp, which also makes any older codes invalid.

## 3. Ask for a code

```http [TodoApi.http]
### Forgot password
POST {{TodoApi_HostAddress}}/forgotPassword
Content-Type: application/json

{ "email": "dave@example.com" }
```

The response is always `200 OK` with an empty body, even when the address is unknown or not confirmed. That stops strangers from using the endpoint to find out who has an account. The mail only goes out when the user exists and has confirmed their email.

## 4. Send the new password

```http [TodoApi.http]
### Reset password
POST {{TodoApi_HostAddress}}/resetPassword
Content-Type: application/json

{
  "email": "dave@example.com",
  "resetCode": "paste-the-code-from-the-email",
  "newPassword": "N3w-Passw0rd!"
}
```

On success you get `200 OK`. The new password must pass the same rules as at registration, and a weak one returns `400` with the reasons.

## 5. Try it

1. Start smtp4dev with `docker run --rm -p 3000:80 -p 2525:25 rnwood/smtp4dev`.
2. Register `dave@example.com` and open the confirmation link from smtp4dev, as in [Send a welcome email](/web-api/howto-welcome-email).
3. Send the `Forgot password` request. A message titled "Your password reset code" appears at `http://localhost:3000`.
4. Paste the code into the `Reset password` request and send it. You get `200 OK`.
5. Log in with `N3w-Passw0rd!`. You get an `accessToken`.

Send the same reset request a second time and the code is refused.

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "InvalidToken": ["Invalid token."] }
}
```

The same error comes back for a code older than two hours or one typed wrong.

::: warning
Anyone with the code can take over the account until it expires. Do not log the email body, and keep the lifespan short.
:::

## Learn more

- [Send a welcome email](/web-api/howto-welcome-email)
- [Sending email](/web-api/email)
- [Security hardening](/web-api/security-hardening)
- [Account confirmation and password recovery in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/accconfirm)
