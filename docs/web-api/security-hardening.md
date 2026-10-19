# Secrets and hardening

## Introduction

An API that works on your machine can still leak secrets, accept oversized requests, or hand attackers a stack trace. This page is a checklist of settings to review before you put an API on the public internet. Each item is small, and most are configuration rather than code.

## Keep secrets out of source control

```bash
dotnet user-secrets set "Email:ApiKey" "dev-key-123"
```

In Development, store secrets with user secrets, which live in your user profile instead of the project folder. The [Configuration](/web-api/configuration) page covers the setup. `appsettings.json` is committed with your code, so it should never hold passwords, API keys, or signing keys.

In production, set the same keys as environment variables. A double underscore stands for the `:` separator, so `ConnectionStrings__Default` overrides `ConnectionStrings:Default`. Your host, such as Azure App Service or Kubernetes secrets, injects them at startup.

For secrets you rotate or share across apps, use a vault. On Azure that is Key Vault.

```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Identity
```

```csharp [Program.cs (excerpt)]
using Azure.Identity;

if (builder.Environment.IsProduction())
{
    builder.Configuration.AddAzureKeyVault(
        new Uri("https://todoapi-vault.vault.azure.net/"),
        new DefaultAzureCredential());
}
```

Key Vault secrets become ordinary configuration values. A secret named `ConnectionStrings--Default` maps to `ConnectionStrings:Default`. `DefaultAzureCredential` uses the app's managed identity in Azure, so there is no password for the vault itself.

## HTTPS and HSTS

```csharp [Program.cs (excerpt)]
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
```

HSTS is a response header that tells browsers to use only HTTPS for your domain from then on. Redirection helps browsers, but API clients often do not follow redirects, and a client that sent a token over plain HTTP has already leaked it. The better fix is to not listen on HTTP at all in production. When a reverse proxy or load balancer terminates TLS, configure HTTPS there and keep the app on its internal port.

## Security headers for APIs

```csharp [Program.cs (excerpt)]
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next();
});
```

`X-Content-Type-Options: nosniff` stops browsers from guessing a content type, so a JSON response can never be run as a script or rendered as HTML. Turning off the `Server` header hides that you run Kestrel, which removes one easy hint for automated scanners.

Browser apps need a Content Security Policy (CSP), a header that lists where scripts and styles may load from. An API that returns only JSON does not render HTML, so CSP has nothing to protect. Add one if the same app also serves pages, such as Swagger UI or a SPA from `wwwroot`.

## JWT settings to check

```csharp [Program.cs (excerpt)]
builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.TokenValidationParameters.ValidateIssuer = true;
    options.TokenValidationParameters.ValidateAudience = true;
    options.TokenValidationParameters.ValidateLifetime = true;
    options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(1);
});
```

Change your existing `AddJwtBearer()` call to this one rather than adding a second, because registering the handler twice fails at startup. All three `Validate` settings default to `true`. Write them out anyway, so nobody turns one off during debugging and forgets. `ClockSkew` is the grace period after a token expires, and its default of five minutes is longer than most APIs want. Keep access tokens short-lived, around 5 to 15 minutes, and use refresh tokens for longer sessions. See [Authentication](/web-api/authentication) for the rest of the setup.

## Never return stack traces

The developer exception page shows source code and stack traces. It only runs in Development, and it must stay that way. In every other environment, `UseExceptionHandler` with `AddProblemDetails` returns a plain `500` ProblemDetails body that includes a `traceId` and nothing else. The [Errors](/web-api/errors) page sets this up.

::: warning
Check that `ASPNETCORE_ENVIRONMENT` is not set to `Development` on your servers. A single wrong environment variable turns the developer exception page back on.
:::

## Request size limits

```csharp [Program.cs (excerpt)]
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1_000_000;
});
```

Kestrel accepts request bodies up to about 30 MB by default. A todo API never needs that much, and large bodies cost memory and CPU to parse. Set a limit that fits your largest real request, here 1 MB. Endpoints that accept files need a higher limit, covered on [File uploads](/web-api/file-uploads).

## Vulnerable packages

```bash
dotnet list package --vulnerable --include-transitive
```

This lists every package in the project with a known security advisory, including packages your packages depend on. `dotnet restore` also warns about them, so treat those warnings as errors in your [CI build](/web-api/ci-cd).

## CORS

CORS only matters when browsers call your API from another origin. Allow the exact origins you need and never combine `AllowAnyOrigin` with credentials. The [CORS](/web-api/cors) page covers the policy setup.

## Learn more

- [Safe storage of app secrets in development](https://learn.microsoft.com/aspnet/core/security/app-secrets)
- [Enforce HTTPS in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl)
