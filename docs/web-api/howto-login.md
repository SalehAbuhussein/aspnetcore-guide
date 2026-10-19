# Add login

## Introduction

At the end of this guide `TodoApi` has `POST /register` and `POST /login` endpoints, hands out bearer tokens, and rejects calls to a protected endpoint that come without one. This is the shortest working path, using ASP.NET Core Identity's ready-made API endpoints.

Before you start, you need `TodoApi` with `AppDbContext` and SQLite from [Database](/web-api/database) and [Migrations](/web-api/migrations).

<ApiStyle />

## 1. Install the Identity package

```bash
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
```

This package stores users, hashed passwords, and roles in your database through EF Core.

## 2. Add the user tables to AppDbContext

```csharp [Data/AppDbContext.cs]
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace TodoApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Todo> Todos => Set<Todo>();
}
```

`IdentityDbContext<IdentityUser>` is a `DbContext` that already knows the Identity tables, such as `AspNetUsers`. If you override `OnModelCreating`, start it with `base.OnModelCreating(modelBuilder);` or EF Core cannot build those tables.

```bash
dotnet ef migrations add AddIdentity
dotnet ef database update
```

## 3. Register Identity and map its endpoints

<div class="only-minimal">

```csharp [Program.cs]
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddValidation();

builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<IdentityUser>();
app.MapTodoEndpoints();

app.MapGet("/me", (HttpContext context) => context.User.Identity?.Name)
    .RequireAuthorization();

app.Run();
```

</div>

<div class="only-controllers">

```csharp [Program.cs]
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllers();

builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<IdentityUser>();
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

`MapIdentityApi` adds Minimal API endpoints. They sit next to your controllers without any extra setup.

</div>

`AddIdentityApiEndpoints` registers Identity and makes its bearer token scheme the default way to authenticate. `MapIdentityApi` adds `/register`, `/login`, `/refresh`, `/confirmEmail`, `/forgotPassword`, `/resetPassword`, and `/manage/info`. The `/me` endpoint only lets signed-in callers through.

## 4. Protect your own endpoints

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
var group = app.MapGroup("/todos")
    .RequireAuthorization();
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("todos")]
[Authorize]
public class TodosController(AppDbContext db) : ControllerBase
```

</div>

Every `/todos` endpoint now needs a token. [Authorization](/web-api/authorization) shows how to open single endpoints with `AllowAnonymous` and add policies.

## 5. Try it

```http [TodoApi.http]
@TodoApi_HostAddress = http://localhost:5000

### Register
POST {{TodoApi_HostAddress}}/register
Content-Type: application/json

{ "email": "alice@example.com", "password": "Passw0rd!" }

### Log in
POST {{TodoApi_HostAddress}}/login
Content-Type: application/json

{ "email": "alice@example.com", "password": "Passw0rd!" }

### Who am I?
GET {{TodoApi_HostAddress}}/me
Authorization: Bearer paste-the-accessToken-here
```

`/register` returns `200 OK` with an empty body. The password must pass Identity's default rules: at least 6 characters with an uppercase letter, a lowercase letter, a digit, and a symbol. A weak password gets `400` with the reasons.

`/login` returns the token.

```json
{
  "tokenType": "Bearer",
  "accessToken": "CfDJ8Kc68gsa3vJH...",
  "expiresIn": 3600,
  "refreshToken": "CfDJ8Kc68gsa3vJH..."
}
```

Copy `accessToken` into the `/me` request and send it. You get `alice@example.com`. Remove the `Authorization` line and you get `401 Unauthorized`.

The access token lasts one hour. When it runs out, send `POST /refresh` with `{ "refreshToken": "..." }` to get a new pair without asking for the password again.

::: warning
Identity's bearer tokens are not JWTs. Only this app can read them, because they are encrypted with its data protection keys. That is fine when your own front end calls your own API. If other services must check the tokens, or you already use a login provider such as Microsoft Entra ID or Auth0, use JWT bearer authentication instead, as shown on [Authentication](/web-api/authentication).
:::

## Learn more

- [Authentication](/web-api/authentication) for JWT bearer tokens and `dotnet user-jwts`
- [Authorization](/web-api/authorization) for policies and roles
- [Add an admin role](/web-api/howto-admin-role)
- [Send a welcome email](/web-api/howto-welcome-email)
- [Use Identity to secure a Web API backend for SPAs](https://learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization)
