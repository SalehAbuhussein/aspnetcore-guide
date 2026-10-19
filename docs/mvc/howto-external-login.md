# Sign in with Google

## Introduction

At the end of this guide, the BookShelf login page has a Google button, and people can sign in with their Google account instead of a password. Before you start, you need login from [Add login](/mvc/howto-login), a Google account, and user secrets as described in [Configuration](/mvc/configuration).

## 1. Install the package

```bash
dotnet add package Microsoft.AspNetCore.Authentication.Google
```

This package adds the Google handler. It talks to Google with OAuth 2.0, the standard that lets Google confirm who the user is without your app seeing their Google password.

## 2. Find your HTTPS port

Open `Properties/launchSettings.json` and find the `https` profile.

```json [Properties/launchSettings.json (excerpt)]
"applicationUrl": "https://localhost:7241;http://localhost:5241"
```

Your numbers differ. Google needs the exact HTTPS address, so note the first port.

## 3. Create an OAuth client in Google Cloud

1. Open the [Google Cloud console](https://console.cloud.google.com/) and create a project, or pick an existing one.
2. Go to APIs & Services, then OAuth consent screen. Choose External, fill in the app name and your email, and save. While the app is in testing, add your own Google address under Test users.
3. Go to APIs & Services, then Credentials. Click Create credentials, then OAuth client ID.
4. Choose Web application as the type.
5. Under Authorized redirect URIs, add `https://localhost:7241/signin-google`, with your port from step 2.
6. Click Create, and copy the Client ID and Client secret.

`/signin-google` is the path the Google handler listens on. Google sends the user back there after they approve. If the URI does not match exactly, Google shows "Error 400: redirect_uri_mismatch". For production, add a second redirect URI with your real domain.

## 4. Store the keys in user secrets

```bash
dotnet user-secrets init
dotnet user-secrets set "Authentication:Google:ClientId" "<your-client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<your-client-secret>"
```

The client secret is a password for your app. User secrets keep it out of your repository and only load in Development. In production, set the environment variables `Authentication__Google__ClientId` and `Authentication__Google__ClientSecret` instead.

## 5. Register the Google handler

```csharp [Program.cs (excerpt)]
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    });
```

`AddDefaultIdentity` already sets up cookie sign-in, so `AddAuthentication()` here only adds Google on top. You do not pass a default scheme. The `!` tells the compiler the values are there. If they are missing, the app throws at startup with a message that `ClientId` must be provided, which is what you want.

No migration is needed. Identity already has an `AspNetUserLogins` table that links a Google account to a local user.

## 6. Try it

Run `dotnet run --launch-profile https` and open `https://localhost:7241/Identity/Account/Login`. On the right, under "Use another service to log in", you now see a Google button.

1. Click Google and choose your account on Google's page.
2. Google sends you back to `/signin-google`, and Identity shows the "Associate your Google account" page with your email filled in.
3. Click Register. With `RequireConfirmedAccount` on, confirm the email as you did for normal accounts.
4. Log out and click Google again. This time you are signed in straight away.

The new row in `AspNetUserLogins` has `LoginProvider` set to `Google`. A user can also link Google to an existing password account from Manage account, then External logins.

## Learn more

- [Add login](/mvc/howto-login)
- [Authentication](/mvc/authentication)
- [Configuration](/mvc/configuration)
- [Google external login setup in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/social/google-logins)
