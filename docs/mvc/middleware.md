# Middleware

## Introduction

Middleware is code that runs on every request before it reaches your controller and on every response on its way back. ASP.NET Core chains middleware together into a pipeline. This page shows how the pipeline works and how to write your own middleware.

## The pipeline

Picture the pipeline as a line of steps. A request enters at the top of `Program.cs`, passes through each `app.Use...` call in order, reaches a controller action, and then the response travels back up through the same steps in reverse.

Each middleware can do work before the next step, call the next step, and do work after it returns. A middleware can also stop the request early by writing a response and not calling the next step. The static files middleware does this when it finds a matching file.

## The template's middleware

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
```

Every line between `builder.Build()` and `app.Run()` shapes the pipeline:

- `UseExceptionHandler` catches errors from later steps and shows an error page. See [Error handling](/mvc/errors).
- `UseHsts` tells browsers to use HTTPS for your site in future visits.
- `UseHttpsRedirection` redirects plain HTTP requests to HTTPS.
- `UseRouting` matches the URL to an endpoint, the piece of code that will handle it. See [Routing](/mvc/routing).
- `UseAuthorization` checks whether the user may reach that endpoint. See [Authorization](/mvc/authorization).

`MapStaticAssets` and `MapControllerRoute` add endpoints, the final destinations for requests, such as your CSS files and controller actions.

## Order matters

Middleware runs in the order you add it. `UseAuthorization` must come after `UseRouting`, because it needs to know which endpoint was matched. `UseExceptionHandler` goes near the top, because it can only catch errors from steps that come after it.

::: warning
If you put `UseAuthorization` before `UseRouting`, your `[Authorize]` attributes stop working. Keep the template's order and add new middleware with care.
:::

## Inline middleware

```csharp [Program.cs (excerpt)]
using System.Diagnostics;

var app = builder.Build();

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();

    await next(context);

    stopwatch.Stop();
    app.Logger.LogInformation("{Method} {Path} took {Elapsed} ms",
        context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
});
```

Put the `using` line at the top of the file and the `app.Use` call right after `builder.Build()`. `app.Use` adds a middleware written as a lambda, a short unnamed function. `context` holds the request and response. `next` is the rest of the pipeline. Code before `await next(context)` runs on the way in, and code after it runs on the way out, so the stopwatch measures everything that happens later in the pipeline. The message appears in the console, as described on the [Logging](/mvc/logging) page.

## Writing a middleware class

When middleware grows past a few lines, move it into a class.

```csharp [Middleware/RequestTimingMiddleware.cs]
using System.Diagnostics;

namespace BookShelf.Middleware;

public class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await next(context);

        logger.LogInformation("{Method} {Path} took {Elapsed} ms",
            context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
    }
}
```

```csharp [Program.cs (excerpt)]
using BookShelf.Middleware;

app.UseMiddleware<RequestTimingMiddleware>();
```

A middleware class needs a constructor that takes a `RequestDelegate`, which is the next step in the pipeline, and a public `InvokeAsync` method that takes an `HttpContext`. ASP.NET Core creates the class once when the app starts. Add the `UseMiddleware` line where you want it to run, such as right after `builder.Build()`.

::: tip
Because the class is created once, inject scoped services as extra parameters of `InvokeAsync`, not in the constructor. `AppDbContext`, which you meet on the [Database](/mvc/database) page, is one of them.
:::

## Learn more

- [ASP.NET Core middleware](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/)
- [Write custom ASP.NET Core middleware](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/write)
