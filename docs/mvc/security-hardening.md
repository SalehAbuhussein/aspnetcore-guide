# Secrets and hardening

## Introduction

The template gives you safe defaults, but a public app needs more. Passwords leak through Git history, browsers need headers that tell them what to trust, and running several servers breaks cookies unless they share keys. Work through this page before BookShelf goes on the internet. For an app that only runs on your laptop, the secrets section is the part that matters.

## Keeping secrets out of source control

A secret is any value that gives access to something, such as a database password, an API key, or an SMTP login. Never put secrets in `appsettings.json` or any file you commit. Once a secret is in Git history, treat it as public and rotate it.

### In development, use user secrets

```bash
dotnet user-secrets init
dotnet user-secrets set "Shelf:ApiKey" "dev-only-key"
dotnet user-secrets set "Smtp:Password" "app-password-here"
```

User secrets store values in a JSON file in your user profile, outside the project folder. ASP.NET Core loads them automatically when the environment is Development, and they override `appsettings.json`. Your code reads them the normal way, through `builder.Configuration`. See [Configuration](/mvc/configuration).

### In production, use environment variables or a vault

On a single server or in a container, environment variables are fine, as shown on the [Deployment](/mvc/deployment) page. Set them in the host's settings, not in a script you commit.

For apps on Azure, Azure Key Vault keeps secrets in one place with access logs and rotation.

```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Identity
```

```csharp [Program.cs (excerpt)]
using Azure.Identity;

if (builder.Environment.IsProduction())
{
    builder.Configuration.AddAzureKeyVault(
        new Uri("https://bookshelf-kv.vault.azure.net/"),
        new DefaultAzureCredential());
}
```

`DefaultAzureCredential` signs in with the app's managed identity in Azure, so no password is needed to reach the vault. A Key Vault secret named `ConnectionStrings--Default` becomes the `ConnectionStrings:Default` setting.

## HTTPS and HSTS

```csharp [Program.cs (excerpt)]
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
```

`UseHttpsRedirection` sends plain HTTP requests to the HTTPS address. HSTS (HTTP Strict Transport Security) is a header that tells the browser to use HTTPS for your domain from now on, even if the user types `http://`. The template already calls `UseHsts` outside Development. The default max age is 30 days, and a year is common once you know HTTPS works.

::: warning
Only raise the HSTS max age or add `IncludeSubDomains` when every subdomain serves HTTPS. Browsers remember the header, and you cannot take it back early.
:::

## Security headers

A small middleware adds the headers that browsers use to block common attacks.

```csharp [Middleware/SecurityHeadersMiddleware.cs]
namespace BookShelf.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' https://cdn.jsdelivr.net; " +
            "img-src 'self' data:; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "frame-ancestors 'none'";

        return next(context);
    }
}
```

```csharp [Program.cs (excerpt)]
using BookShelf.Middleware;

app.UseMiddleware<SecurityHeadersMiddleware>();
```

`X-Content-Type-Options: nosniff` stops the browser from guessing a file's type, so an uploaded text file cannot run as a script. `Referrer-Policy` limits how much of your URL is sent to other sites when users click a link. `Content-Security-Policy`, or CSP, lists where scripts, images, and other resources may load from. If an attacker injects a `<script>` tag, the browser refuses to run it.

This CSP allows scripts from your own site and the jsdelivr CDN used on the [Real-time with SignalR](/mvc/real-time) page. It blocks inline `<script>` blocks, so move page scripts into files under `wwwroot/js`. Start strict, open the browser console, and add sources only when something you trust is blocked. Add the middleware right after `builder.Build()` so every response gets the headers, including error pages.

## Antiforgery recap

Every POST action in BookShelf has `[ValidateAntiForgeryToken]`, and the form tag helper adds the hidden token. Forgetting the attribute on one action leaves a hole. Apply the check to every unsafe request instead.

```csharp [Program.cs (excerpt)]
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
```

`AutoValidateAntiforgeryTokenAttribute` validates the token on POST, PUT, PATCH, and DELETE, and skips GET. It needs `using Microsoft.AspNetCore.Mvc;`.

## Data Protection keys

ASP.NET Core encrypts the authentication cookie and antiforgery tokens with Data Protection keys. By default each server creates its own keys. Run two copies behind a load balancer, and a cookie made by one is garbage to the other, so users get logged out at random. Containers have the same problem on every restart, because the keys live inside the container.

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.DataProtection;

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/bookshelf/keys"))
    .SetApplicationName("BookShelf");
```

`PersistKeysToFileSystem` stores keys in a folder that every instance can reach, such as a shared volume. `SetApplicationName` makes sure all instances accept each other's keys. On Azure, you can store keys in Blob Storage and protect them with Key Vault through extra packages.

## Cookie settings

```csharp [Program.cs (excerpt)]
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
```

`ConfigureApplicationCookie` changes the Identity sign-in cookie from the [Authentication](/mvc/authentication) page. `HttpOnly` hides the cookie from JavaScript, so a script injection cannot steal it. `SecurePolicy = Always` sends it over HTTPS only. `SameSite = Lax` keeps the browser from sending it on most cross-site requests. `ExpireTimeSpan` with `SlidingExpiration` logs users out after eight hours without activity.

## Checking packages for known vulnerabilities

```bash
dotnet list package --vulnerable --include-transitive
```

This command checks every NuGet package your project uses, including packages pulled in by other packages, against the GitHub advisory database. `dotnet restore` also warns about vulnerable packages by default. Run the command in your CI pipeline and update any package it lists. See [CI/CD](/mvc/ci-cd).

## Learn more

- [Safe storage of app secrets in development](https://learn.microsoft.com/aspnet/core/security/app-secrets)
- [Configure ASP.NET Core Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)
