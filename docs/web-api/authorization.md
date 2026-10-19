# Authorization

## Introduction

Authorization decides what a known user is allowed to do. It runs after [authentication](/web-api/authentication), which identifies the user. ASP.NET Core lets you protect single endpoints, whole route groups, and specific actions with roles and policies.

<ApiStyle />

## Requiring a signed-in user

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs]
public static void MapTodoEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/todos")
        .RequireAuthorization();

    group.MapGet("/", GetAll);
    group.MapGet("/{id:int}", GetById);
    group.MapPost("/", Create);
    group.MapPut("/{id:int}", Update);
    group.MapDelete("/{id:int}", Delete);
}
```

`RequireAuthorization()` on a group applies to every endpoint inside it. A request without a valid token now gets `401 Unauthorized`. You can also call it on one endpoint, for example `app.MapGet("/me", ...).RequireAuthorization()`.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using Microsoft.AspNetCore.Authorization;

namespace TodoApi.Controllers;

[ApiController]
[Route("todos")]
[Authorize]
public class TodosController(AppDbContext db) : ControllerBase
{
    // GetAll, GetById, Create, Update, and Delete stay as they are
}
```

`[Authorize]` on the controller class applies to every action inside it. A request without a valid token now gets `401 Unauthorized`. You can also put `[Authorize]` on a single action instead of the whole class.

</div>

This needs `builder.Services.AddAuthorization()` in `Program.cs`, which the [Authentication](/web-api/authentication) page already added.

## Allowing anonymous access

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs]
var group = app.MapGroup("/todos")
    .RequireAuthorization();

group.MapGet("/", GetAll)
    .AllowAnonymous();
```

`AllowAnonymous()` opens one endpoint inside a protected group. Anonymous means the caller sent no credentials. Here anyone can list todos, but only signed-in users can create, change, or delete them.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[ApiController]
[Route("todos")]
[Authorize]
public class TodosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<Todo>>> GetAll() =>
        await db.Todos.ToListAsync();
}
```

`[AllowAnonymous]` opens one action inside a protected controller. Anonymous means the caller sent no credentials. Here anyone can list todos, but only signed-in users can create, change, or delete them.

</div>

## Creating policies

```csharp [Program.cs]
builder.Services.AddAuthentication().AddJwtBearer();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("admin", policy => policy.RequireRole("Admin"))
    .AddPolicy("todos:write", policy => policy.RequireClaim("scope", "todos:write"));
```

A policy is a named set of rules a user must pass. `RequireRole` checks the user's role claims, and `RequireClaim` checks for a claim with a given value. `AddAuthorizationBuilder()` registers authorization services and returns a builder, so you do not also need `AddAuthorization()`.

## Applying policies

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs]
var group = app.MapGroup("/todos")
    .RequireAuthorization();

group.MapGet("/", GetAll)
    .AllowAnonymous();
group.MapGet("/{id:int}", GetById);
group.MapPost("/", Create);
group.MapPut("/{id:int}", Update);
group.MapDelete("/{id:int}", Delete)
    .RequireAuthorization("admin");
```

Pass a policy name to `RequireAuthorization`. The `DELETE` endpoint now requires a signed-in user from the group and the `admin` policy from its own call. A signed-in user who fails the policy gets `403 Forbidden`, which means "I know who you are, but you cannot do this."

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

Pass a policy name with `[Authorize(Policy = "admin")]`. The `Delete` action now requires a signed-in user from the class attribute and the `admin` policy from its own attribute. Both must pass. A signed-in user who fails the policy gets `403 Forbidden`, which means "I know who you are, but you cannot do this."

</div>

Create a token with the right role to test it.

```bash
dotnet user-jwts create --name alice --role Admin
dotnet user-jwts create --name bob
```

Alice can delete todos. Bob gets `403`.

::: tip
Prefer policies over checking roles inside each endpoint. A policy keeps the rule in one place, so you change it once when the requirements change.
:::

## Reading the current user

<div class="only-minimal">

```csharp [Program.cs]
using System.Security.Claims;

app.MapGet("/me", (ClaimsPrincipal user) => new
{
    Name = user.Identity?.Name,
    IsAdmin = user.IsInRole("Admin"),
    Claims = user.Claims.Select(c => new { c.Type, c.Value })
})
.RequireAuthorization();
```

A `ClaimsPrincipal` is the object that represents the current user and all their claims. Add it as a parameter and ASP.NET Core fills it in from the request. `Identity.Name` returns the user name, `IsInRole` checks a role, and `FindFirstValue(ClaimTypes.NameIdentifier)` reads the user id when your token includes one.

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
        Name = User.Identity?.Name,
        IsAdmin = User.IsInRole("Admin"),
        Claims = User.Claims.Select(c => new { c.Type, c.Value })
    });
}
```

A `ClaimsPrincipal` is the object that represents the current user and all their claims. Every controller has one in its `User` property, filled in from the request. `Identity.Name` returns the user name, `IsInRole` checks a role, and `User.FindFirstValue(ClaimTypes.NameIdentifier)` reads the user id when your token includes one.

</div>

Use the user id to scope data, for example storing an `OwnerId` on each todo and filtering queries with it.

## Authorization in controllers

```csharp [Controllers/AdminController.cs]
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Policy = "admin")]
public class AdminController : ControllerBase
{
    [HttpGet("stats")]
    public IActionResult Stats() => Ok(new { Users = 2 });

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok();
}
```

<div class="only-minimal">

If you use [controllers](/web-api/controllers), the `[Authorize]` and `[AllowAnonymous]` attributes do the same job as the methods above.

</div>

<div class="only-controllers">

Attributes on the class apply to every action, and attributes on an action add to them. Here every action needs the `admin` policy, except `Health`, which `[AllowAnonymous]` opens to everyone.

</div>

## Learn more

- [Authentication and authorization in Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/security)
- [Policy-based authorization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/policies)
