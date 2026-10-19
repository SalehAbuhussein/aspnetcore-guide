# Authentication

## Introduction

Authentication answers the question "who is calling?" An API usually checks a token sent with each request. This page uses JWT bearer tokens, which is the most common choice for APIs that serve single page apps and mobile clients.

Deciding what a caller is allowed to do is a separate step called authorization. That is covered on the [Authorization](/web-api/authorization) page.

<ApiStyle />

## What a JWT is

A JWT, or JSON Web Token, is a signed string that holds claims about a user. A claim is one piece of information, such as a user name or a role. The server checks the signature to confirm nobody changed the token. "Bearer" means whoever holds the token can use it, so treat tokens like passwords.

## Installing the package

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

This package adds the JWT bearer handler, the code that reads a token from a request and turns it into a user.

## Configuring JWT bearer authentication

This is the whole file. If you added lines on earlier pages, such as the seeding options from [Seeding](/web-api/seeding), keep them.

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
// Keep the other lines you already have, such as AddOpenApi and UseHttpsRedirection.
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddValidation();

builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapTodoEndpoints();
app.MapProjectEndpoints();

app.MapGet("/me", (HttpContext context) => context.User.Identity?.Name)
    .RequireAuthorization();

app.Run();
```

</div>

<div class="only-controllers">

```csharp [Program.cs (excerpt)]
// Keep the other lines you already have, such as AddOpenApi and UseHttpsRedirection.
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllers();

builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
```

```csharp [Controllers/MeController.cs]
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public class MeController : ControllerBase
{
    [HttpGet]
    public string? Get() => User.Identity?.Name;
}
```

</div>

`AddAuthentication().AddJwtBearer()` registers the JWT handler. When it is the only scheme, it becomes the default. `UseAuthentication` reads the token on each request, and `UseAuthorization` then checks whether the endpoint allows the user. Keep them in this order, after routing middleware and before your endpoints run.

<div class="only-minimal">

`RequireAuthorization()` on `/me` means only callers with a valid token get through. Everyone else gets `401 Unauthorized`.

</div>

<div class="only-controllers">

`[Authorize]` on `MeController` means only callers with a valid token get through. Everyone else gets `401 Unauthorized`. `User` is a property of `ControllerBase` that holds the current user.

</div>

`AddJwtBearer()` with no arguments reads its settings from the `Authentication:Schemes:Bearer` section of configuration. The next section fills that section in for local development.

## Creating test tokens with user-jwts

```bash
dotnet user-jwts create
```

The `user-jwts` tool creates a signed token for local testing, so you do not need a real login server while you build. It stores a signing key in [user secrets](/web-api/configuration) and adds the issuer and audience to `appsettings.Development.json`. The command prints the token at the end of its output.

You can add a name, roles, or custom claims.

```bash
dotnet user-jwts create --name alice --role Admin
dotnet user-jwts list
```

`list` shows every token you have created for this project. Tokens from this tool only work in the Development environment.

## Sending the token

```http [TodoApi.http]
@TodoApi_HostAddress = http://localhost:5000
@token = paste-your-token-here

### Who am I?
GET {{TodoApi_HostAddress}}/me
Authorization: Bearer {{token}}
```

Clients send the token in the `Authorization` header with the word `Bearer`, a space, and then the token. If the token is valid, `/me` returns the name in the token. For the token from `dotnet user-jwts create --name alice`, that is `alice`. Remove the header and you get `401`.

The same request with curl looks like this.

```bash
curl -i http://localhost:5000/me -H "Authorization: Bearer <token>"
```

::: warning
In production, tokens come from a real identity provider such as Microsoft Entra ID, Auth0, or your own token service. Set `Authority` and `Audience` for that provider in configuration, and never commit signing keys to source control.
:::

## Identity API endpoints

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.Identity;

// before builder.Build()
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<AppDbContext>();

// after builder.Build()
app.MapIdentityApi<IdentityUser>();
```

If you want your API to manage its own users, ASP.NET Core Identity can add ready-made `/register`, `/login`, and related endpoints. Identity is the built-in user management system that stores users, hashed passwords, and roles in your database. It needs the `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package, and `AppDbContext` must inherit from `IdentityDbContext<IdentityUser>` instead of `DbContext`. Your `OnModelCreating` must then start with `base.OnModelCreating(modelBuilder);`, or EF Core cannot build the Identity tables. After you change the base class, add a migration with `dotnet ef migrations add AddIdentity` and apply it with `dotnet ef database update` to create the user tables.

`AddIdentityApiEndpoints` makes Identity's own bearer scheme the default. The `AddJwtBearer` handler is no longer the default, so the `user-jwts` token from earlier stops working. Identity issues its own tokens, so most APIs pick either Identity or an external JWT provider.

## Learn more

- [Configure JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [Manage JSON Web Tokens in development with dotnet user-jwts](https://learn.microsoft.com/aspnet/core/security/authentication/jwt-authn)
