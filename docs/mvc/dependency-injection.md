# Dependency injection

## Introduction

Dependency injection, or DI, means a class asks for the objects it needs instead of creating them itself. ASP.NET Core has a DI container built in, and the framework uses it everywhere. Most of the pages that follow rely on it, so it pays to understand it early.

## Why use DI

A dependency is any object your class needs to do its work, such as a database context or an email sender. If a controller writes `new BookService()`, it is tied to that exact class. If it asks for an `IBookService` in its constructor, you can swap the implementation in one place, and you can pass in a fake version when testing.

The container is the part of ASP.NET Core that keeps a list of services and builds them on request. A service is any class you register with the container.

## Creating a service

Start with an interface, which is a list of methods a class promises to provide.

```csharp [Services/IBookService.cs]
using BookShelf.Models;

namespace BookShelf.Services;

public interface IBookService
{
    Task<List<Book>> GetAllAsync();
    Task<Book?> FindAsync(int id);
    Task<Book> CreateAsync(Book book);
}
```

Then write a class that implements it.

```csharp [Services/BookService.cs]
using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public class BookService(AppDbContext db) : IBookService
{
    public Task<List<Book>> GetAllAsync() =>
        db.Books.OrderBy(b => b.Title).ToListAsync();

    public async Task<Book?> FindAsync(int id) =>
        await db.Books.FindAsync(id);

    public async Task<Book> CreateAsync(Book book)
    {
        db.Books.Add(book);
        await db.SaveChangesAsync();
        return book;
    }
}
```

`AppDbContext` is the class that talks to the database. You create it on the [Database](/mvc/database) page, so treat it here as one more service the container knows how to build. `BookService` asks for it in its constructor, and the container passes one in when it builds the service.

## Registering the service

```csharp [Program.cs (excerpt)]
using BookShelf.Services;

builder.Services.AddScoped<IBookService, BookService>();
```

This line tells the container "when someone asks for `IBookService`, give them a `BookService`." Put it with the other `builder.Services` calls, before `builder.Build()`.

## Service lifetimes

A lifetime controls how long the container keeps an instance before it makes a new one. There are three.

```csharp [Program.cs (excerpt)]
// Each service gets exactly one of these.
builder.Services.AddTransient<ISlugMaker, SlugMaker>();    // new every time
builder.Services.AddScoped<IBookService, BookService>();   // one per request
builder.Services.AddSingleton<IClock, SystemClock>();      // one for the app
```

`ISlugMaker` and `IClock` stand in for your own small services. Write them the same way as `IBookService`: an interface plus a class.

`AddTransient` creates a new instance every time something asks for it. Use it for small, stateless helpers.

`AddScoped` creates one instance per HTTP request and shares it within that request. Use it for anything that works with `AppDbContext`, since `AddDbContext` registers the context as scoped too.

`AddSingleton` creates one instance for the whole life of the app and shares it with every request. Use it for caches and settings that do not change. A singleton runs on many requests at once, so its code must be thread safe.

::: warning
Do not inject a scoped service, such as `AppDbContext`, into a singleton. The singleton would hold on to one context forever. In Development, ASP.NET Core throws an error when it detects this.
:::

For `BookService`, `AddScoped` is the right choice, because it depends on `AppDbContext`.

## Constructor injection in controllers

```csharp [Controllers/BooksController.cs]
using BookShelf.Models;
using BookShelf.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookShelf.Controllers;

public class BooksController(IBookService books) : Controller
{
    public async Task<IActionResult> Index()
    {
        return View(await books.GetAllAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var book = await books.FindAsync(id);
        if (book is null) return NotFound();
        return View(book);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Book book)
    {
        if (!ModelState.IsValid) return View(book);

        await books.CreateAsync(book);
        return RedirectToAction(nameof(Index));
    }
}
```

The primary constructor lists `IBookService`, and the container fills it in for each request. The controller no longer knows about EF Core. See [Controllers](/mvc/controllers) for more on controller classes.

::: tip
This controller needs the database and views from later pages. Come back to it after [CRUD](/mvc/crud). Its `Index` passes a `List<Book>`, which matches the list view from that page. The search and paging view from [Querying](/mvc/querying) expects a `BookListViewModel` instead. The Part 2 pages build on this short version.
:::

## Injecting into views

```razor [Views/Shared/_BookCount.cshtml]
@using BookShelf.Services
@inject IBookService BookService

@{
    var allBooks = await BookService.GetAllAsync();
}
<footer>BookShelf has @allBooks.Count books.</footer>
```

`@inject` asks the container for a service and gives it a name you can use in the view. You could render this partial view from the layout with `<partial name="_BookCount" />`. Keep this for small view helpers. Most data should still come from the controller through the model.

## Learn more

- [Dependency injection in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection)
- [Dependency injection into views](https://learn.microsoft.com/aspnet/core/mvc/views/dependency-injection)
