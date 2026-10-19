# Models

## Introduction

A model is a plain C# class that holds data. In MVC, models carry data from the controller to the view, and from a submitted form back to the controller.

## The Book model

You created this class on the [Controllers](/mvc/controllers) page:

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

Each `{ get; set; }` line is a property, a value on the object that code can read and change. The `= ""` gives each string a starting value, so it is never `null`. That keeps the compiler's nullable warnings quiet.

The model has no methods that talk to a web request or a view. It only describes a book. That makes it easy to reuse in controllers, views, and the database layer.

`Id` is the book's unique number. When you add a database on the [Database](/mvc/database) page, Entity Framework Core treats a property named `Id` as the primary key by convention.

## Entities and view models

As your app grows, you will have two kinds of model classes.

- An entity is a class that maps to a database table. `Book` is an entity. Its shape follows how you store data.
- A view model is a class shaped for one view. It holds exactly what that page needs to display or submit.

Passing entities straight to views works for simple pages, and the early pages of this track do it. A view model is better when:

- the page needs data from more than one source, such as books plus a search term
- the page should not expose every field of the entity
- a form has fields that are not stored, such as a "confirm" checkbox

::: tip
Put view models in `Models/` with a `ViewModel` suffix, such as `BookListViewModel`. Some teams use a separate `ViewModels/` folder. Pick one and stay consistent.
:::

## A view model example

The book list page needs the books, the current search term, and a total count. Create a view model for it:

```csharp [Models/BookListViewModel.cs]
namespace BookShelf.Models;

public class BookListViewModel
{
    public List<Book> Books { get; set; } = [];
    public string? Search { get; set; }
    public int TotalCount { get; set; }
}
```

`[]` is a collection expression that creates an empty list. `string?` means the search term can be `null` when the user has not searched.

Build it in the controller:

```csharp [Controllers/BooksController.cs]
public IActionResult Index(string? search)
{
    var matches = string.IsNullOrWhiteSpace(search)
        ? _books
        : _books.Where(b => b.Title.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

    var model = new BookListViewModel
    {
        Books = matches,
        Search = search,
        TotalCount = _books.Count
    };

    return View(model);
}
```

The `search` parameter comes from the query string, so `/Books?search=dune` sets it to `"dune"`. `_books` is the in-memory list from the [Controllers](/mvc/controllers) page.

Then use it in the view:

```razor [Views/Books/Index.cshtml]
@model BookListViewModel

<h1>Books</h1>

<form method="get">
    <input name="search" value="@Model.Search" placeholder="Search by title" />
    <button type="submit">Search</button>
</form>

<p>Showing @Model.Books.Count of @Model.TotalCount books.</p>

@foreach (var book in Model.Books)
{
    <partial name="_BookCard" model="book" />
}
```

The view declares `@model BookListViewModel`, so every property is type checked. The `_BookCard` partial comes from [Layouts and partials](/mvc/layouts).

## Where models go next

Models also drive forms and validation. On the [Forms](/mvc/forms) page, MVC fills a `Book` from submitted fields. On the [Validation](/mvc/validation) page, you add attributes like `[Required]` to its properties.

## Learn more

- [Model binding in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/models/model-binding)
- [Get started with ASP.NET Core MVC: add a model](https://learn.microsoft.com/aspnet/core/tutorials/first-mvc-app/adding-model)
