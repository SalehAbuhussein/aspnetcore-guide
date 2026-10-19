# Versioning

## Introduction

Once a mobile app or a partner depends on your API, you cannot rename a field or change a response shape without breaking them. Versioning lets old clients keep using the old contract while new clients move to the new one. You need it only for breaking changes to an API with clients you do not control. Adding a new endpoint or a new optional field is not breaking, so it does not need a new version. If you ship the API and its only client together, skip versioning and change both at once.

<ApiStyle />

## What counts as breaking

1. Removing or renaming a field, endpoint, or query parameter.
2. Changing a field's type, such as `id` from a number to a string.
3. Making an optional input required, or adding a new validation rule that rejects old requests.
4. Changing status codes clients rely on, such as 404 becoming 200 with `null`.

## URL segment versioning

The simplest approach puts the version in the URL, `/v1/todos` and `/v2/todos`, and needs no package.

<div class="only-minimal">

Your existing `MapTodoEndpoints` maps `/todos` on whatever builder you call it on, so a route group adds the prefix:

```csharp [Program.cs (excerpt)]
using TodoApi.Endpoints;

var v1 = app.MapGroup("/v1").WithGroupName("v1");
v1.MapTodoEndpoints();

var v2 = app.MapGroup("/v2").WithGroupName("v2");
v2.MapTodoEndpointsV2();
```

Say v2 returns todos with a `status` string instead of `isDone`. Write only the endpoints that changed:

```csharp [Endpoints/TodoEndpointsV2.cs]
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Endpoints;

public record TodoV2(int Id, string Title, string Status);

public static class TodoEndpointsV2
{
    public static void MapTodoEndpointsV2(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/todos");
        group.MapGet("/", GetAll);
    }

    static async Task<Ok<List<TodoV2>>> GetAll(AppDbContext db, CancellationToken ct)
    {
        var todos = await db.Todos
            .Select(t => new TodoV2(t.Id, t.Title, t.IsDone ? "done" : "open"))
            .ToListAsync(ct);

        return TypedResults.Ok(todos);
    }
}
```

Both versions read the same database. Only the shape on the wire differs. When most of v2 is identical, call the shared handler methods from both files instead of copying them.

</div>

<div class="only-controllers">

With controllers, the version goes in the route attribute. Change the route on your existing `TodosController` to `v1/todos`:

```csharp [Controllers/TodosController.cs (excerpt)]
[ApiController]
[Route("v1/todos")]
[ApiExplorerSettings(GroupName = "v1")]
public class TodosController(AppDbContext db) : ControllerBase
```

Say v2 returns todos with a `status` string instead of `isDone`. Add a second controller with only the actions that changed:

```csharp [Controllers/TodosV2Controller.cs]
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Controllers;

public record TodoV2(int Id, string Title, string Status);

[ApiController]
[Route("v2/todos")]
[ApiExplorerSettings(GroupName = "v2")]
public class TodosV2Controller(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TodoV2>>> GetAll(CancellationToken ct)
    {
        var todos = await db.Todos
            .Select(t => new TodoV2(t.Id, t.Title, t.IsDone ? "done" : "open"))
            .ToListAsync(ct);

        return Ok(todos);
    }
}
```

Both versions read the same database. Only the shape on the wire differs. `[ApiExplorerSettings]` gives each controller a group name, which the OpenAPI section below uses. When most of v2 is identical, move the shared logic into a service both controllers call instead of copying it.

</div>

This is the approach to start with. It is easy to see in logs, easy to test with a browser, and easy to route at a proxy.

## One OpenAPI document per version

```csharp [Program.cs (excerpt)]
builder.Services.AddOpenApi("v1");
builder.Services.AddOpenApi("v2");
```

<div class="only-minimal">

Each call registers a document with that name. By default, a document includes the endpoints whose group name matches it, which is what `WithGroupName("v1")` set above. `MapOpenApi()` then serves `/openapi/v1.json` and `/openapi/v2.json`. See [OpenAPI](/web-api/openapi) for adding a UI on top.

</div>

<div class="only-controllers">

Each call registers a document with that name. By default, a document includes the endpoints whose group name matches it, which is what `[ApiExplorerSettings(GroupName = "v1")]` set above. `MapOpenApi()` then serves `/openapi/v1.json` and `/openapi/v2.json`. See [OpenAPI](/web-api/openapi) for adding a UI on top.

</div>

## Marking a version as deprecated

<div class="only-minimal">

When v2 is ready, change the v1 group to send deprecation headers on every response:

```csharp [Program.cs (excerpt)]
var v1 = app.MapGroup("/v1")
    .WithGroupName("v1")
    .AddEndpointFilter(async (context, next) =>
    {
        var headers = context.HttpContext.Response.Headers;
        headers["Deprecation"] = "@1790812800";
        headers["Sunset"] = "Thu, 01 Apr 2027 00:00:00 GMT";
        headers.Link = "</v2/todos>; rel=\"successor-version\"";
        return await next(context);
    });
```

</div>

<div class="only-controllers">

When v2 is ready, send deprecation headers on every v1 response with an action filter:

```csharp [Filters/DeprecatedVersionAttribute.cs]
using Microsoft.AspNetCore.Mvc.Filters;

namespace TodoApi.Filters;

public class DeprecatedVersionAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers["Deprecation"] = "@1790812800";
        headers["Sunset"] = "Thu, 01 Apr 2027 00:00:00 GMT";
        headers.Link = "</v2/todos>; rel=\"successor-version\"";
    }
}
```

```csharp [Controllers/TodosController.cs (excerpt)]
using TodoApi.Filters;

[ApiController]
[Route("v1/todos")]
[ApiExplorerSettings(GroupName = "v1")]
[DeprecatedVersion]
public class TodosController(AppDbContext db) : ControllerBase
```

An action filter runs code before and after each action of the controller it decorates. Deriving from `ActionFilterAttribute` lets you apply it as an attribute.

</div>

`Deprecation` says the version is deprecated and since when, as a Unix timestamp with an `@` in front, here 1 October 2026. `Sunset` says when it will stop working. Clients and API gateways can watch for these headers and warn their developers. Tell your clients directly too, because few of them read response headers.

## The Asp.Versioning package

<div class="only-minimal">

```bash
dotnet add package Asp.Versioning.Http
```

When you need versions in a header or query string, or many versions with reporting, use the `Asp.Versioning.Http` package. It is the standard versioning library for ASP.NET Core and lives under the .NET Foundation.

```csharp [Program.cs (excerpt)]
using Asp.Versioning;

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

var app = builder.Build();

var versions = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .HasApiVersion(new ApiVersion(2, 0))
    .ReportApiVersions()
    .Build();

var todos = app.MapGroup("/v{version:apiVersion}/todos")
    .WithApiVersionSet(versions);

todos.MapGet("/", GetAllV1).MapToApiVersion(new ApiVersion(1, 0));
todos.MapGet("/", GetAllV2).MapToApiVersion(new ApiVersion(2, 0));
```

`AddApiVersioning` registers the services. `NewApiVersionSet` lists the versions the group supports, and `MapToApiVersion` ties each handler to one of them. The `{version:apiVersion}` route segment reads the version from the URL. Swap the reader for `QueryStringApiVersionReader` or `HeaderApiVersionReader` to read it from `?api-version=2.0` or a header instead. With `ReportApiVersions`, every response carries an `api-supported-versions` header, and `HasDeprecatedApiVersion` adds versions to `api-deprecated-versions`.

`GetAllV1` and `GetAllV2` stand for your handler methods. The package needs more setup than route groups, so add it only when you have outgrown them.

</div>

<div class="only-controllers">

```bash
dotnet add package Asp.Versioning.Mvc
```

When you need versions in a header or query string, or many versions with reporting, use the `Asp.Versioning.Mvc` package. It is the controller flavor of the standard versioning library for ASP.NET Core, which lives under the .NET Foundation.

```csharp [Program.cs (excerpt)]
using Asp.Versioning;

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc();
```

This one controller replaces `TodosController` and `TodosV2Controller` from the URL segment section. Move the `TodoV2` record to `Models/TodoV2.cs` first, with the namespace `TodoApi.Models`.

```csharp [Controllers/TodosController.cs]
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("v{version:apiVersion}/todos")]
public class TodosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<List<Todo>>> GetAllV1(CancellationToken ct) =>
        Ok(await db.Todos.ToListAsync(ct));

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<List<TodoV2>>> GetAllV2(CancellationToken ct) =>
        Ok(await db.Todos
            .Select(t => new TodoV2(t.Id, t.Title, t.IsDone ? "done" : "open"))
            .ToListAsync(ct));
}
```

`AddApiVersioning` registers the services and `AddMvc` connects them to controllers. `[ApiVersion]` lists the versions the controller supports, and `[MapToApiVersion]` ties each action to one of them. The `{version:apiVersion}` route segment reads the version from the URL. Swap the reader for `QueryStringApiVersionReader` or `HeaderApiVersionReader` to read it from `?api-version=2.0` or a header instead. With `ReportApiVersions`, every response carries an `api-supported-versions` header, and `[ApiVersion("1.0", Deprecated = true)]` adds a version to `api-deprecated-versions`.

The package versions every controller in the app. A controller without `[ApiVersion]` gets the default version, and without `AssumeDefaultVersionWhenUnspecified` its unversioned URLs return 404. The package needs more setup than separate controllers, so add it only when you have outgrown them.

</div>

::: tip
Version the whole API, not single endpoints. A client should be able to say "I use v1" and get a consistent contract.
:::

## Learn more

- [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview)
- [Generate OpenAPI documents](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi)
