# Organizing a larger app

## Introduction

The default `Controllers`, `Models`, and `Views` folders work well for a dozen files. Once BookShelf has thirty controllers and an admin section, finding things gets slow and every change touches three far-apart folders. This page shows a few moves that keep a growing app easy to change, and a list of patterns to avoid. If your app still fits on one screen of folders, leave it as it is.

## Signs the default layout is crowded

You scroll a long list to find a controller. Changing one page means opening files in `Controllers`, `Models`, and `Views/Books`. Admin screens and public screens share a folder and a layout they do not need. When two of these are true, it is time to split things up.

## Areas for big sections

An area is a separate section of the app with its own controllers and views, such as an admin panel. Use areas when a section has a different audience, layout, or set of permissions.

```text
Areas/
  Admin/
    Controllers/
      BooksController.cs
    Views/
      _ViewImports.cshtml
      _ViewStart.cshtml
      Books/
        Index.cshtml
```

```csharp [Areas/Admin/Controllers/BooksController.cs]
using BookShelf.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookShelf.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class BooksController(IBookService books) : Controller
{
    public async Task<IActionResult> Index()
    {
        return View(await books.GetAllAsync());
    }
}
```

`[Area("Admin")]` tells MVC this controller belongs to the Admin area, so it looks for views in `Areas/Admin/Views`. Two controllers named `BooksController` can live side by side because their namespaces differ. Copy `_ViewImports.cshtml` from `Views` into the area so tag helpers work there too.

Add a route for areas before the default route.

```csharp [Program.cs (excerpt)]
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
```

Now `/Admin/Books` goes to the admin controller and `/Books` goes to the public one. Link to an area with `asp-area="Admin"` on a tag helper. See [Routing](/mvc/routing) for how patterns match.

## Feature folders as an alternative

Some teams group by feature instead: a `Features/Books` folder holding the controller, view models, and service together. It reads well, but MVC looks for views in `Views/{controller}` by default, so you have to write a custom view location expander. Prefer areas for big sections and keep the default layout inside them. It needs no extra setup and every .NET developer knows it.

## Thin controllers

A controller should read the request, call a service, and pick a result. Business rules belong in services, as you started on the [Dependency injection](/mvc/dependency-injection) page.

```csharp [Services/BookService.cs (excerpt)]
public async Task<bool> AddReviewAsync(int bookId, int rating, string body)
{
    var exists = await db.Books.AnyAsync(b => b.Id == bookId);
    if (!exists) return false;

    db.Reviews.Add(new Review { BookId = bookId, Rating = rating, Body = body });
    await db.SaveChangesAsync();
    return true;
}
```

Add the method to `IBookService` too. The controller action shrinks to one call and a redirect, and you can test the rule without a web request.

## A view model per page

A view model is a class shaped for one view, holding exactly what that page shows. Passing the `Book` entity straight to a page is fine early on. Once a page needs data from several places, give it its own class.

```csharp [Models/BookDetailsViewModel.cs]
namespace BookShelf.Models;

public class BookDetailsViewModel
{
    public required Book Book { get; init; }
    public double AverageRating { get; init; }
    public List<Review> LatestReviews { get; init; } = [];
}
```

A view model also stops over-posting, where a user adds extra form fields to change properties you never meant to expose.

## Keeping Program.cs short

As you add services, `Program.cs` grows into a long list. Move the registrations into an extension method.

```csharp [Extensions/ServiceCollectionExtensions.cs]
using BookShelf.Data;
using BookShelf.Services;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBookShelfServices(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite(config.GetConnectionString("Default")));

        services.AddScoped<IBookService, BookService>();

        return services;
    }
}
```

```csharp [Program.cs (excerpt)]
using BookShelf.Extensions;

builder.Services.AddControllersWithViews();
builder.Services.AddBookShelfServices(builder.Configuration);
```

Remove the old `AddDbContext` and `AddScoped` lines from `Program.cs` so nothing is registered twice. If your `AddDbContext` call has the `UseSeeding` callbacks from the [Seeding](/mvc/seeding) page, move them into the method with it. Split into more methods, such as `AddBookShelfAuth`, only when one gets long.

## What not to add

Tutorials and templates often push layers that cost more than they give. Skip these until you have the problem they solve.

1. No generic repository over EF Core. `DbSet<Book>` already is a repository, and `DbContext` already is a unit of work. An `IRepository<T>` wrapper hides `Include`, `AsNoTracking`, and projections, so you end up adding methods until it is a worse copy of EF Core.
2. No unit-of-work wrapper. `SaveChangesAsync` already saves every change in one transaction. Wrapping it adds a file and nothing else.
3. No interface for every class. An interface earns its place when you have two implementations or need to fake the class in tests, like `IBookService` or an email sender. A view model or a small helper does not need one.
4. No CQRS or mediator library until you feel the pain. Sending every call through a mediator turns "go to definition" into a search. Plain services handle most apps for years.

::: tip
When you are unsure, write the direct code first. Adding a layer later is easy. Removing one that every file depends on is not.
:::

## Learn more

- [Areas in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/controllers/areas)
- [Common web application architectures](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
