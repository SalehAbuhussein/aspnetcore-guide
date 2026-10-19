# Controllers

## Introduction

A controller is a C# class that handles requests for one area of your app. Each public method on it is an action, and each action returns a result such as a page, a redirect, or a 404.

## Create a controller

The controller works with books, so it needs a `Book` class first. Create `Models/Book.cs`. The [Models](/mvc/models) page explains it.

```csharp [Models/Book.cs]
namespace BookShelf.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int Year { get; set; }
}
```

Then create `Controllers/BooksController.cs`:

```csharp [Controllers/BooksController.cs]
using BookShelf.Models;
using Microsoft.AspNetCore.Mvc;

namespace BookShelf.Controllers;

public class BooksController : Controller
{
    // Temporary in-memory store. The database pages replace this.
    private static readonly List<Book> _books =
    [
        new() { Id = 1, Title = "Dune", Author = "Frank Herbert", Year = 1965 },
        new() { Id = 2, Title = "Kindred", Author = "Octavia E. Butler", Year = 1979 },
        new() { Id = 3, Title = "The Hobbit", Author = "J. R. R. Tolkien", Year = 1937 },
    ];

    public IActionResult Index()
    {
        return View(_books);
    }

    public IActionResult Details(int id)
    {
        var book = _books.FirstOrDefault(b => b.Id == id);

        if (book is null)
        {
            return NotFound();
        }

        return View(book);
    }
}
```

`BooksController` inherits from `Controller`, the base class that gives you helper methods like `View()` and `NotFound()`. The list is `static`, so it lives as long as the app runs. Changes are lost on restart. The [Database](/mvc/database) page swaps it for `AppDbContext`.

With the default route, `/Books` runs `Index` and `/Books/Details/2` runs `Details(2)`. The `id` parameter is filled from the URL by model binding, which is the process of copying request data into method parameters.

## Naming rules

MVC finds controllers and views by name, so follow these rules:

- The class is `public` and its name ends in `Controller`. The URL uses the name without the suffix, so `BooksController` is `/Books`.
- Actions are `public` methods. Private methods are never reachable by URL.
- `View()` looks for `Views/{Controller}/{Action}.cshtml`, so `BooksController.Details` renders `Views/Books/Details.cshtml`. If that file is missing, MVC checks `Views/Shared/`.

## Action results

`IActionResult` is an interface that every result type implements. Returning it lets one action send back different kinds of responses. Here are the helpers you will use most:

| Helper | What it does |
| --- | --- |
| `View()` | Renders the view that matches the action name |
| `View(model)` | Renders that view and passes it data |
| `View("Other", model)` | Renders a view with a different name |
| `RedirectToAction("Index")` | Sends the browser to another action |
| `NotFound()` | Returns a 404 status |
| `BadRequest()` | Returns a 400 status |
| `Content("text")` | Returns plain text |

`RedirectToAction` takes an action name, and optionally a controller name and route values:

```csharp
return RedirectToAction("Details", new { id = book.Id });
return RedirectToAction("Index", "Home");
```

## Pass data to a view

The usual way is to pass a model object to `View()`, as `Index` and `Details` do above. The view declares the type with `@model` and reads it with `Model`. See [Views](/mvc/views).

For small extra values, such as a page title, use `ViewData`. It is a dictionary that the controller and view share for one request.

```csharp
public IActionResult Index()
{
    ViewData["Title"] = "All books";
    return View(_books);
}
```

Prefer a model for anything the page depends on. `ViewData` values are not type checked, so a typo in a key fails without an error.

## Handle POST requests

By default an action answers any HTTP method. Add `[HttpGet]` or `[HttpPost]` to limit it. A common pattern is two actions with the same name, one to show a form and one to receive it:

```csharp [Controllers/BooksController.cs]
[HttpGet]
public IActionResult Create()
{
    return View();
}

[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult Create(Book book)
{
    book.Id = _books.Count == 0 ? 1 : _books.Max(b => b.Id) + 1;
    _books.Add(book);
    return RedirectToAction(nameof(Index));
}
```

The POST action takes a `Book`. Model binding fills its properties from the submitted form fields. `nameof(Index)` produces the string `"Index"` and is checked by the compiler. The [Forms](/mvc/forms) page explains `[ValidateAntiForgeryToken]` and builds the view.

::: warning
Never change data in a GET action. Browsers, crawlers, and link previews can send GET requests without the user clicking anything. Use POST for anything that creates, updates, or deletes.
:::

## Learn more

- [Handle requests with controllers in ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/mvc/controllers/actions)
