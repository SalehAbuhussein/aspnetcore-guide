# Error handling

## Introduction

Things go wrong in every app. A database goes down, a user types a bad URL, or your code throws an exception. ASP.NET Core shows detailed errors to you while developing and friendly pages to your users in production.

## The developer exception page

When the app runs in the Development environment and an action throws an exception, you see the developer exception page. It shows the error message, the stack trace, the request headers, and the route values. An exception is .NET's way of signaling an error that stops the current code.

You do not need to add anything for this page. ASP.NET Core turns it on automatically when the environment is Development, which is the default when you use `dotnet run`. See [Configuration](/mvc/configuration) for how environments work.

::: warning
Never show the developer exception page in production. It reveals your code, file paths, and settings to anyone who triggers an error.
:::

## The production error page

```csharp [Program.cs (excerpt)]
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
```

The MVC template already contains this block. Outside Development, `UseExceptionHandler` catches any unhandled exception, logs it, and runs the `/Home/Error` action instead. The user sees a plain error page with a status code of 500.

```csharp [Controllers/HomeController.cs (excerpt)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public IActionResult Error()
{
    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
```

The template's `Error` action shows `Views/Shared/Error.cshtml` with a request id. A user can report that id, and you can find the matching entry in your [logs](/mvc/logging). `ResponseCache` makes sure browsers never cache the error page.

To test the production page on your machine, set the environment before running.

```bash
dotnet run -e ASPNETCORE_ENVIRONMENT=Production
```

`-e` sets an environment variable for this run only, and it wins over the `Development` value in `launchSettings.json`.

## Status code pages

An exception is not the only kind of error. A request for a missing page returns a 404 status code, a number that tells the browser the page was not found. By default the browser shows its own blank page for that.

```csharp [Program.cs (excerpt)]
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

app.UseHttpsRedirection();
```

Add the line after the closing brace of the `if (app.Environment.IsDevelopment())` block, before `UseHttpsRedirection`. Outside the block, it runs in every environment. When a response has an error status code and no body, the middleware runs the request again at `/Home/StatusCode/404`, with `{0}` replaced by the code. The original status code is kept, so the browser still receives a 404.

## A custom 404 view

```csharp [Controllers/HomeController.cs (excerpt)]
[ActionName("StatusCode")]
public IActionResult StatusCodePage(int id)
{
    if (id == 404)
    {
        return View("NotFound");
    }

    return View("StatusCode", id);
}
```

The default route maps `/Home/StatusCode/404` to an action named `StatusCode` with `id` set to 404. The C# method has a different name because `Controller` already has a `StatusCode` method. `[ActionName("StatusCode")]` gives the action the URL name you want.

```razor [Views/Home/NotFound.cshtml]
@{
    ViewData["Title"] = "Page not found";
}

<h1>We could not find that page</h1>
<p>The book or page you asked for does not exist.</p>
<a asp-controller="Books" asp-action="Index">Browse all books</a>
```

```razor [Views/Home/StatusCode.cshtml]
@model int

<h1>Something went wrong</h1>
<p>The server returned status code @Model.</p>
```

These views use your normal [layout](/mvc/layouts), so error pages look like the rest of the site.

## Returning NotFound from actions

Your `Details` action already does this. Here is the pattern on its own:

```csharp
public async Task<IActionResult> Details(int id)
{
    var book = await db.Books.FindAsync(id);
    if (book is null)
    {
        return NotFound();
    }

    return View(book);
}
```

`NotFound()` returns an empty response with status code 404. The status code pages middleware sees it and shows your custom 404 view. Other helpers include `BadRequest()` for 400 and `Forbid()` for 403.

::: tip
Return `NotFound()` for missing records instead of throwing an exception. A missing book is an expected case, not a bug.
:::

## Learn more

- [Handle errors in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling)
