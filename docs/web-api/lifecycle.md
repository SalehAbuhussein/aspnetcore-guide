# Request lifecycle

## Introduction

When a request misbehaves, you need to know which step handled it. Was it rejected by authorization, failed during binding, or broke in your handler? This page follows one request from the URL to the JSON response and shows where you can plug in your own code. Read it once now for the big picture. The pages that follow fill in each step, and you can come back here when a request does something you did not expect.

<ApiStyle />

## From URL to JSON

Take `GET /todos/5`. Here is what happens, in order.

| Step | What runs | What it does |
| --- | --- | --- |
| 1 | Kestrel | The built-in web server reads the raw HTTP request from the network and builds an `HttpContext`. |
| 2 | Middleware pipeline | Each middleware in `Program.cs` runs in the order you added it, such as exception handling, HTTPS redirection, CORS, authentication and authorization. |
| 3 | Routing | The routing middleware compares the URL and method to every mapped endpoint and picks the best match, here `GET /todos/{id:int}`. |
| 4 | Endpoint selection | The chosen endpoint and its metadata, such as an authorization requirement, are attached to the `HttpContext`. Later middleware like authorization reads that metadata. |
| 5 | Parameter binding | ASP.NET Core fills the handler or action parameters. `id` comes from the route, `AppDbContext` from DI, a DTO from the JSON body. |
| 6 | Filters | Filters run around the handler. Validation runs here too. |
| 7 | Handler | Your method runs and returns a result such as `200 OK` with the todo. |
| 8 | Result execution | The result sets the status code and headers, and `System.Text.Json` writes the object as JSON. |
| 9 | Back out | The response passes back through the middleware in reverse order, then Kestrel sends it. |

`WebApplication` adds the routing middleware at the start of the pipeline and runs the matched endpoint at the end, so you rarely call `UseRouting` yourself. That is why `UseAuthorization` can see which endpoint was chosen even though the handler has not run yet.

`AppDbContext`, the class that talks to the database, arrives on the [Database](/web-api/database) page, and tokens on the [Authentication](/web-api/authentication) page. You do not need either to follow the steps.

If any step stops the request, the later steps never run. An unauthenticated request ends at step 2 with a 401. A body that fails validation ends at step 6 with a 400, and your handler is never called.

## Middleware or filter

Middleware sees every request, including ones that match no endpoint, and works with the raw `HttpContext`. Filters only run for endpoints and can see the bound arguments and the handler result. Use [middleware](/web-api/middleware) for things that apply to the whole app, and filters for things that apply to a group of endpoints.

## Filters

<div class="only-minimal">

```csharp [Endpoints/TimingFilter.cs]
using System.Diagnostics;

namespace TodoApi.Endpoints;

public class TimingFilter(ILogger<TimingFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var start = Stopwatch.GetTimestamp();

        var result = await next(context);

        var elapsed = Stopwatch.GetElapsedTime(start);
        logger.LogInformation(
            "{Method} {Path} took {Elapsed} ms",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            elapsed.TotalMilliseconds);

        return result;
    }
}
```

An endpoint filter wraps the handler. Code before `next(context)` runs before the handler, and code after it runs once the handler has returned. You can also return early without calling `next`, for example to reject a request. The filter is created through DI, so it can ask for `ILogger<T>` or any other service.

Apply it to the whole `/todos` group:

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
var group = app.MapGroup("/todos")
    .AddEndpointFilter<TimingFilter>();
```

Every endpoint in the group now logs its timing. Filters added to a group run before filters added to a single endpoint.

You can read the bound arguments inside a filter with `context.GetArgument<T>(index)` or `context.Arguments`. For one-off logic, `AddEndpointFilter` also takes a lambda with the same `(context, next)` shape.

</div>

<div class="only-controllers">

```csharp [Filters/TimingActionFilter.cs]
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TodoApi.Filters;

public class TimingActionFilter(ILogger<TimingActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var start = Stopwatch.GetTimestamp();
        await next();
        logger.LogInformation(
            "{Action} took {Elapsed} ms",
            context.ActionDescriptor.DisplayName,
            Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}
```

An action filter wraps the action. Code before `next()` runs before the action, and code after it runs once the action has returned. You can also set `context.Result` and skip `next()`, for example to reject a request. The filter is created through DI, so it can ask for `ILogger<T>` or any other service.

Apply it to every action in the app:

```csharp [Program.cs (excerpt)]
using TodoApi.Filters;

builder.Services.AddControllers(options =>
    options.Filters.Add<TimingActionFilter>());
```

To apply it to one controller instead, register it with `builder.Services.AddScoped<TimingActionFilter>();` and add an attribute:

```csharp [Controllers/TodosController.cs (excerpt)]
using TodoApi.Filters;

[ApiController]
[Route("todos")]
[ServiceFilter<TimingActionFilter>]
public class TodosController(AppDbContext db) : ControllerBase
```

Every action in `TodosController` now logs its timing. You can read the bound arguments inside a filter with `context.ActionArguments`. The `[ApiController]` validation that returns a 400 also runs as a filter, before yours.

</div>

## Result serialization

Handlers return objects, and `System.Text.Json` turns them into JSON. Property names are camelCase by default.

<div class="only-minimal">

To change the settings for every Minimal API response, configure `JsonOptions`:

```csharp [Program.cs (excerpt)]
using System.Text.Json.Serialization;

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

This writes enums as their names instead of numbers. Controllers have their own settings under `AddControllers().AddJsonOptions(...)`.

</div>

<div class="only-controllers">

To change the settings for every controller response, chain `AddJsonOptions` onto `AddControllers`:

```csharp [Program.cs (excerpt)]
using System.Text.Json.Serialization;

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

This writes enums as their names instead of numbers. Minimal APIs use a separate setting, `ConfigureHttpJsonOptions`.

</div>

::: tip
Later, on the [Relationships](/web-api/relationships) page, you add entities that point at each other. Returning an EF Core entity that has navigation properties, such as `Project.Todos` and `Todo.Project`, can cause a cycle error during serialization. Return DTOs instead. See [EF Core performance](/web-api/ef-performance) for projecting into DTOs.
:::

## Learn more

- [Filters in Minimal API apps](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/min-api-filters)
- [ASP.NET Core Middleware](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/)
