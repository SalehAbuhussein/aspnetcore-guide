# Middleware

## Introduction

Middleware is code that runs on every request before and after your endpoint. Each piece of middleware can inspect the request, change the response, or stop the request early. ASP.NET Core uses middleware for HTTPS redirects, authentication, CORS, error handling, and more.

<ApiStyle />

## The pipeline

<div class="only-minimal">

```csharp [Program.cs]
var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapTodoEndpoints();

app.Run();
```

</div>

<div class="only-controllers">

```csharp [Program.cs]
var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
```

</div>

The pipeline is the chain of middleware a request passes through. Each `Use...` call adds one link, and requests flow through them in the order you write them. The response then travels back out in reverse order.

This is the pipeline TodoApi ends up with. You add CORS, authentication, authorization, and rate limiting on later pages, so a new project has fewer lines.

Order matters. The exception handler goes first so it can catch errors from everything after it. Authentication must come before authorization, because you need to know who the user is before you check what they can do.

## Writing inline middleware

<div class="only-minimal">

```csharp [Program.cs]
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();

    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Elapsed-Ms"] = stopwatch.ElapsedMilliseconds.ToString();
        return Task.CompletedTask;
    });

    await next(context);

    app.Logger.LogInformation("{Method} {Path} took {Elapsed} ms",
        context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
});

app.MapGet("/", () => "Hello");

app.Run();
```

</div>

<div class="only-controllers">

```csharp [Program.cs]
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();

    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Elapsed-Ms"] = stopwatch.ElapsedMilliseconds.ToString();
        return Task.CompletedTask;
    });

    await next(context);

    app.Logger.LogInformation("{Method} {Path} took {Elapsed} ms",
        context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
});

app.MapControllers();

app.Run();
```

</div>

`app.Use` adds middleware written as a lambda. The `context` holds the request and response, and `next` calls the rest of the pipeline. Code before `await next(context)` runs on the way in, and code after it runs on the way out.

Headers must be set before the response starts sending. `OnStarting` registers a callback that runs right before that moment, so the `X-Elapsed-Ms` header is safe to add there. If you skip calling `next`, the request stops at your middleware and never reaches the endpoint.

## Writing a middleware class

```csharp [Middleware/RequestTimingMiddleware.cs]
using System.Diagnostics;

namespace TodoApi.Middleware;

public class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Elapsed-Ms"] = stopwatch.ElapsedMilliseconds.ToString();
            return Task.CompletedTask;
        });

        await next(context);

        logger.LogInformation("{Method} {Path} took {Elapsed} ms",
            context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
    }
}
```

<div class="only-minimal">

```csharp [Program.cs]
using TodoApi.Middleware;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseMiddleware<RequestTimingMiddleware>();

app.MapGet("/", () => "Hello");

app.Run();
```

</div>

<div class="only-controllers">

```csharp [Program.cs]
using TodoApi.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<RequestTimingMiddleware>();

app.MapControllers();

app.Run();
```

</div>

A middleware class takes a `RequestDelegate` in its constructor and has an `InvokeAsync` method. `RequestDelegate` is the type of `next`, the rest of the pipeline. Classes are easier to test and reuse than inline lambdas, so move middleware into a class once it grows past a few lines.

The class is created once when the app starts. Singleton services such as `ILogger<T>` can go in the constructor. For scoped services, such as the `AppDbContext` you create on the [Database](/web-api/database) page, add them as extra parameters of `InvokeAsync` instead.

::: tip

<div class="only-minimal">

Use middleware for things that apply to many endpoints. For logic that belongs to one endpoint or group, endpoint filters added with `AddEndpointFilter` are a better fit.

</div>

<div class="only-controllers">

Use middleware for things that apply to many endpoints. For logic that belongs to one controller or action, action filters are a better fit. Write a class that implements `IAsyncActionFilter`, register it, and apply it with `[ServiceFilter(typeof(YourFilter))]`.

</div>

:::

## Built-in middleware

ASP.NET Core ships middleware for the most common jobs. You turn each one on with a `Use...` call.

| Method | What it does |
| --- | --- |
| `UseExceptionHandler` | Catches unhandled errors and returns a clean response. See [Error handling](/web-api/errors). |
| `UseHttpsRedirection` | Redirects HTTP requests to HTTPS. |
| `UseCors` | Adds CORS headers. See [CORS](/web-api/cors). |
| `UseAuthentication` | Reads the token and sets the user. See [Authentication](/web-api/authentication). |
| `UseAuthorization` | Checks access rules. See [Authorization](/web-api/authorization). |
| `UseRateLimiter` | Limits how often clients can call you. See [Rate limiting](/web-api/rate-limiting). |
| `UseHttpLogging` | Logs request and response details. See [Logging](/web-api/logging). |

`WebApplication` also adds a few middleware for you, such as routing and, in Development, the developer exception page. You do not need to call those yourself.

## Learn more

- [ASP.NET Core Middleware](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/)
- [Write custom ASP.NET Core middleware](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/write)
