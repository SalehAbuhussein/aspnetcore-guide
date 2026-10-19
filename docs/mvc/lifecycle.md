# Request lifecycle

## Introduction

Every request to BookShelf passes through the same steps in the same order. This page walks one request from the URL to the HTML and shows where your own code can plug in. Read it once now for the big picture. The pages that follow fill in each step, and you can come back here when something runs at a time you did not expect.

## The steps in order

Take a browser asking for `GET /Books/Details/5`.

| Step | What happens |
| --- | --- |
| 1. Kestrel | The built-in web server accepts the connection and turns the raw bytes into an `HttpContext` object. |
| 2. Middleware | Each middleware in `Program.cs` runs in the order you added it: exception handler, HTTPS redirect, static files, and so on. |
| 3. Routing | `UseRouting` matches the URL against your routes and picks an endpoint, here `BooksController.Details`. |
| 4. Auth middleware | `UseAuthentication` reads the cookie. `UseAuthorization` checks `[Authorize]` on the chosen endpoint and can stop the request with a redirect to login or a 403. |
| 5. Endpoint runs | The endpoint hands the request to MVC, which starts the filter pipeline. |
| 6. Authorization filters | MVC-level checks such as antiforgery token validation run. They can stop the request early. |
| 7. Resource filters | Run before model binding. Useful for short-circuiting, such as serving a cached result. |
| 8. Model binding | MVC fills action parameters from the route, query string, and form. `id` becomes `5`. Validation fills `ModelState`. |
| 9. Action filters | Run right before and right after the action method. |
| 10. Action | Your `Details` method runs and returns `View(book)`. |
| 11. Exception filters | Run only if the action or an action filter threw. |
| 12. Result filters | Run before and after the result executes. |
| 13. View rendering | Razor turns `Details.cshtml` and the layout into HTML and writes it to the response. |
| 14. Back out | The response travels back through the middleware in reverse order and Kestrel sends it. |

Middleware wraps everything. Filters only exist inside MVC, so they run for controller actions and never for static files.

You do not need to know every step yet. Logins, cookies, and `[Authorize]` arrive on the [Authentication](/mvc/authentication) and [Authorization](/mvc/authorization) pages, and model binding on the [Forms](/mvc/forms) page.

## Middleware and filters, which to pick

Middleware sees every request, including ones for `/css/site.css`. It knows nothing about controllers or models. Use it for cross-cutting work at the HTTP level, such as security headers or request timing. The [Middleware](/mvc/middleware) page shows how to write one.

Filters run inside MVC and can see the action name, its arguments, and `ModelState`. Use a filter when the logic needs to know about the action, such as rejecting invalid models or logging which action ran.

## Writing an action filter

An action filter runs around the action method. This one logs how long each action takes.

```csharp [Filters/TimingFilter.cs]
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BookShelf.Filters;

public class TimingFilter(ILogger<TimingFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // Runs after model binding, before the action.
        var watch = Stopwatch.StartNew();

        var executed = await next();

        // Runs after the action, before the result (the view) executes.
        watch.Stop();
        logger.LogInformation("{Action} took {Ms} ms",
            context.ActionDescriptor.DisplayName, watch.ElapsedMilliseconds);

        if (executed.Exception is not null)
        {
            logger.LogWarning("{Action} threw", context.ActionDescriptor.DisplayName);
        }
    }
}
```

Code before `await next()` runs after model binding, so `context.ActionArguments` already holds the bound values. Code after it runs once the action returns. If you never call `next()` and set `context.Result` instead, the action does not run at all.

## Registering the filter

Add it globally so it runs for every action.

```csharp [Program.cs (excerpt)]
using BookShelf.Filters;

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<TimingFilter>();
});
```

`Filters.Add<T>` builds the filter through DI, so `ILogger` gets injected. To use it on one controller or action instead, register the class with `builder.Services.AddScoped<TimingFilter>()` and put `[ServiceFilter(typeof(TimingFilter))]` on it.

::: tip
The timing here covers the action only. View rendering happens later, in step 13. To time the whole request, write middleware instead.
:::

## Where the other filters fit

You rarely write the other filter types by hand. `[ValidateAntiForgeryToken]` is an authorization filter. `[Authorize]` is read by the authorization middleware in step 4, before MVC starts. Exception filters exist, but the app-wide exception handler middleware from [Errors](/mvc/errors) covers more cases, including errors thrown during view rendering.

## Learn more

- [Filters in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/controllers/filters)
- [ASP.NET Core middleware](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/)
