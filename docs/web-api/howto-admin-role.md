# Add an admin role

## Introduction

At the end of this guide `TodoApi` has an `Admin` role, an admin account created from configuration when the app starts, and a `DELETE /todos/{id}` endpoint that only admins can call. Other signed-in users get `403 Forbidden`.

Before you start, you need Identity API endpoints from [Add login](/web-api/howto-login). The policy setup follows [Authorization](/web-api/authorization).

<ApiStyle />

## 1. Turn on roles

```csharp [Program.cs (excerpt)]
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();
```

`AddRoles<IdentityRole>()` registers `RoleManager<IdentityRole>` and the role tables. It also swaps in a claims factory that adds a role claim for each of the user's roles when they log in. The `AspNetRoles` tables are already part of `IdentityDbContext`, so you do not need a new migration if you created the Identity tables earlier.

## 2. Seed the admin from configuration

```csharp [Data/AdminSeeder.cs]
using Microsoft.AspNetCore.Identity;

namespace TodoApi.Data;

public static class AdminSeeder
{
    public static async Task SeedAdminAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roles.RoleExistsAsync("Admin"))
        {
            await roles.CreateAsync(new IdentityRole("Admin"));
        }

        var email = app.Configuration["Admin:Email"];
        var password = app.Configuration["Admin:Password"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            return;
        }

        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!await users.IsInRoleAsync(admin, "Admin"))
        {
            await users.AddToRoleAsync(admin, "Admin");
        }
    }
}
```

```csharp [Program.cs (excerpt)]
var app = builder.Build();

await app.SeedAdminAsync();
```

The seeder creates the role, then the admin user, then puts the user in the role. Each step checks first, so running it on every start is safe. `UserManager` hashes the password for you.

```bash
dotnet user-secrets set "Admin:Email" "admin@example.com"
dotnet user-secrets set "Admin:Password" "Admin-Passw0rd!"
```

Keep the password in [user secrets](/web-api/configuration) locally and in an environment variable named `Admin__Password` in production. If either value is missing, the seeder skips the user, so a fresh server never gets a default password.

## 3. Add the policy

```csharp [Program.cs (excerpt)]
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("admin", policy => policy.RequireRole("Admin"));
```

This is the same `admin` policy as on [Authorization](/web-api/authorization). `AddAuthorizationBuilder()` replaces `AddAuthorization()`.

## 4. Apply it to delete

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
var group = app.MapGroup("/todos")
    .RequireAuthorization();

group.MapDelete("/{id:int}", Delete)
    .RequireAuthorization("admin");
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[ApiController]
[Route("todos")]
[Authorize]
public class TodosController(AppDbContext db) : ControllerBase
{
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return NotFound();
        }

        db.Todos.Remove(todo);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
```

</div>

Both rules must pass: the caller is signed in, and the caller has the `Admin` role.

## 5. Try it

Add an endpoint that shows the roles in the token.

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
using System.Security.Claims;

app.MapGet("/me", (ClaimsPrincipal user) => new
{
    name = user.Identity?.Name,
    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
}).RequireAuthorization();
```

</div>

<div class="only-controllers">

```csharp [Controllers/MeController.cs]
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public class MeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        name = User.Identity?.Name,
        roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value)
    });
}
```

</div>

Log in as `admin@example.com` with `POST /login` and call `GET /me` with the token. You get:

```json
{ "name": "admin@example.com", "roles": ["Admin"] }
```

A user who registered normally gets `"roles": []`. Now send `DELETE /todos/1` with each token. The admin gets `204 No Content`, the normal user gets `403 Forbidden`.

Roles are copied into the token at login. If you add someone to a role, they must log in again (or call `/refresh`) before the new role shows up.

## Learn more

- [Authorization](/web-api/authorization)
- [Add login](/web-api/howto-login)
- [Seeding](/web-api/seeding)
- [Role-based authorization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/roles)
