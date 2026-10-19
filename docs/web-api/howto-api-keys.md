# Protect an API with keys

## Introduction

At the end of this guide other services can call `TodoApi` by sending an `X-Api-Key` header. An authentication handler checks the key, so `RequireAuthorization` and `[Authorize]` work the same as they do for users. Only hashes of the keys are stored.

Before you start, you need `TodoApi` from [Building CRUD](/web-api/crud). API keys suit service-to-service calls, such as a billing job reading your data. For people, use [Add login](/web-api/howto-login).

<ApiStyle />

## 1. Create a key and its hash

```bash
KEY=$(openssl rand -base64 32)
echo "Key:  $KEY"
printf %s "$KEY" | openssl dgst -sha256 -binary | base64
```

The first value is the key you give to the calling service once. The second is its SHA-256 hash, which is all `TodoApi` keeps. If your configuration or database leaks, nobody can call the API with a hash. Random keys of 32 bytes are long enough that a fast hash such as SHA-256 is safe here, unlike user passwords.

## 2. Store the hashes

```json [appsettings.json (excerpt)]
{
  "ApiKeys": [
    { "Name": "billing-service", "Hash": "LdUjr6620I8buGXMN3q2rjxqie6mlNQXHVr76ePoizk=" }
  ]
}
```

Each client gets a name, so logs and endpoints can tell callers apart. To revoke a key, delete its entry. With many clients, move this list into a database table with the same two columns.

## 3. Write the authentication handler

```csharp [Auth/ApiKeyHandler.cs]
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TodoApi.Auth;

public class ApiKeyClient
{
    public string Name { get; set; } = "";
    public string Hash { get; set; } = "";
}

public class ApiKeyHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presented = SHA256.HashData(Encoding.UTF8.GetBytes(values.ToString()));
        var clients = configuration.GetSection("ApiKeys").Get<List<ApiKeyClient>>() ?? [];

        foreach (var client in clients)
        {
            var stored = Convert.FromBase64String(client.Hash);
            if (CryptographicOperations.FixedTimeEquals(presented, stored))
            {
                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, client.Name)], SchemeName);
                var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
    }
}
```

An authentication handler turns a request into a user, the same job the JWT handler does on [Authentication](/web-api/authentication). No header means `NoResult`, so other schemes still get a chance. A known key becomes a user whose name is the client name.

`CryptographicOperations.FixedTimeEquals` takes the same time whether the first byte or the last byte differs. A normal `==` stops at the first difference, and an attacker who measures response times could guess a value byte by byte.

## 4. Register the scheme and a policy

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.Authentication;
using TodoApi.Auth;

builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyHandler>(ApiKeyHandler.SchemeName, null);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("apiKey", policy => policy
        .AddAuthenticationSchemes(ApiKeyHandler.SchemeName)
        .RequireAuthenticatedUser());
```

`AddScheme` adds the handler under the name `ApiKey`. The `apiKey` policy runs that handler and requires a successful result. Naming the scheme in the policy means it works next to Identity or JWT bearer, whichever is your default. If keys are the only way in, `AddAuthentication(ApiKeyHandler.SchemeName)` makes it the default and plain `RequireAuthorization()` is enough.

Keep `app.UseAuthentication()` and `app.UseAuthorization()` before your endpoints, as on [Authentication](/web-api/authentication).

## 5. Protect an endpoint

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
using System.Security.Claims;

app.MapGet("/reports/summary", async (ClaimsPrincipal user, AppDbContext db) => new
{
    client = user.Identity?.Name,
    open = await db.Todos.CountAsync(t => !t.IsDone)
}).RequireAuthorization("apiKey");
```

</div>

<div class="only-controllers">

```csharp [Controllers/ReportsController.cs]
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Controllers;

[ApiController]
[Route("reports")]
[Authorize(Policy = "apiKey")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary() => Ok(new
    {
        client = User.Identity?.Name,
        open = await db.Todos.CountAsync(t => !t.IsDone)
    });
}
```

</div>

## 6. Try it

```bash
curl -i http://localhost:5000/reports/summary -H "X-Api-Key: <the key from step 1>"
```

You get `200 OK` with `{"client":"billing-service","open":3}`. A wrong key, no header, or a user's bearer token all get `401 Unauthorized`.

::: warning
Keys travel in every request, so only accept them over HTTPS. Never log the header.
:::

## Learn more

- [Authentication](/web-api/authentication)
- [Authorization](/web-api/authorization)
- [Rate limiting](/web-api/rate-limiting) to limit each client by name
- [Overview of ASP.NET Core authentication](https://learn.microsoft.com/aspnet/core/security/authentication/)
