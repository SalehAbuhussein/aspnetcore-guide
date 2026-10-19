# Building CRUD

## Introduction

CRUD stands for create, read, update, and delete, the four basic things an app does with data. This page builds a `BooksController` that does all four with `AppDbContext`, plus a short Razor view for each action. It assumes you have finished the [Database](/mvc/database) and [Migrations](/mvc/migrations) pages.

## Scaffolding as an option

::: tip
Scaffolding is code generation. It can write a full controller and five views for you. Install the tool once, then run it from the project folder:

```bash
dotnet tool install --global Microsoft.dotnet-scaffold
dotnet scaffold
```

Pick the MVC controller with views option, then choose `Book` and `AppDbContext`. The generated code is long, so this page writes a smaller version by hand. Once you understand each piece, the scaffolded code will make sense.
:::

## The controller

```csharp [Controllers/BooksController.cs]
using BookShelf.Data;
using BookShelf.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

public class BooksController(AppDbContext db) : Controller
{
    // GET: /Books
    public async Task<IActionResult> Index()
    {
        var books = await db.Books.OrderBy(b => b.Title).ToListAsync();
        return View(books);
    }

    // GET: /Books/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var book = await db.Books.FindAsync(id);
        if (book is null) return NotFound();
        return View(book);
    }

    // GET: /Books/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Books/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Book book)
    {
        if (!ModelState.IsValid) return View(book);

        db.Books.Add(book);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: /Books/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var book = await db.Books.FindAsync(id);
        if (book is null) return NotFound();
        return View(book);
    }

    // POST: /Books/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Book book)
    {
        if (id != book.Id) return BadRequest();
        if (!ModelState.IsValid) return View(book);

        db.Books.Update(book);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: /Books/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var book = await db.Books.FindAsync(id);
        if (book is null) return NotFound();
        return View(book);
    }

    // POST: /Books/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var book = await db.Books.FindAsync(id);
        if (book is not null)
        {
            db.Books.Remove(book);
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
```

The primary constructor `BooksController(AppDbContext db)` asks ASP.NET Core for a database context. The framework creates one per request and passes it in. This is [dependency injection](/mvc/dependency-injection).

### Reading

`Index` loads every book with `ToListAsync` and passes the list to the view. `Details` loads one book by `id`. When the book does not exist, it returns `NotFound()`, which sends a 404 response.

### Creating

Create has two actions with the same name. The GET action shows an empty form. The POST action receives the submitted form as a `Book` object through [model binding](/mvc/forms), which fills the object's properties from the form fields.

`ModelState.IsValid` is false when [validation](/mvc/validation) fails, and the action shows the form again with the errors. Otherwise `Add` marks the book as new and `SaveChangesAsync` writes it to the database with an `INSERT`.

After a successful POST, the action redirects to `Index`. This pattern is called Post/Redirect/Get. It stops the browser from submitting the form again when the user refreshes.

### Updating

The Edit GET action loads the book and shows a filled-in form. The POST action checks that the `id` in the URL matches the form, then calls `Update` and `SaveChangesAsync`, which runs an `UPDATE` statement.

### Deleting

The Delete GET action shows a confirmation page. The POST action is named `DeleteConfirmed` because C# cannot have two methods with the same name and the same parameters. `[ActionName("Delete")]` tells MVC to treat it as the `Delete` action anyway.

::: warning
Never delete data from a GET request. Search engines and browser prefetching follow links, so a GET link could wipe out rows. Always delete through a POST form.
:::

`[ValidateAntiForgeryToken]` checks a hidden token that the form tag helper adds to each form. It blocks other sites from submitting forms on behalf of your users.

## The views

Each action has a view in `Views/Books`. See [Views](/mvc/views) for Razor basics.

```razor [Views/Books/Index.cshtml]
@model List<BookShelf.Models.Book>

<h1>Books</h1>
<a asp-action="Create">Add a book</a>

<table>
    @foreach (var book in Model)
    {
        <tr>
            <td>@book.Title</td>
            <td>@book.Author</td>
            <td>@book.Year</td>
            <td>
                <a asp-action="Details" asp-route-id="@book.Id">View</a>
                <a asp-action="Edit" asp-route-id="@book.Id">Edit</a>
                <a asp-action="Delete" asp-route-id="@book.Id">Delete</a>
            </td>
        </tr>
    }
</table>
```

```razor [Views/Books/Details.cshtml]
@model BookShelf.Models.Book

<h1>@Model.Title</h1>
<p>@Model.Author, @Model.Year</p>
<a asp-action="Edit" asp-route-id="@Model.Id">Edit</a>
<a asp-action="Index">Back to list</a>
```

```razor [Views/Books/Create.cshtml]
@model BookShelf.Models.Book

<h1>Add a book</h1>

<form asp-action="Create" method="post">
    <div asp-validation-summary="All"></div>
    <label asp-for="Title"></label>
    <input asp-for="Title" />
    <label asp-for="Author"></label>
    <input asp-for="Author" />
    <label asp-for="Year"></label>
    <input asp-for="Year" />
    <button type="submit">Save</button>
</form>
```

```razor [Views/Books/Edit.cshtml]
@model BookShelf.Models.Book

<h1>Edit @Model.Title</h1>

<form asp-action="Edit" asp-route-id="@Model.Id" method="post">
    <div asp-validation-summary="All"></div>
    <input type="hidden" asp-for="Id" />
    <label asp-for="Title"></label>
    <input asp-for="Title" />
    <label asp-for="Author"></label>
    <input asp-for="Author" />
    <label asp-for="Year"></label>
    <input asp-for="Year" />
    <button type="submit">Save</button>
</form>
```

The hidden `Id` field sends the book's key back with the form, so the POST action knows which row to update.

```razor [Views/Books/Delete.cshtml]
@model BookShelf.Models.Book

<h1>Delete @Model.Title?</h1>

<form asp-action="Delete" asp-route-id="@Model.Id" method="post">
    <button type="submit">Yes, delete it</button>
    <a asp-action="Index">Cancel</a>
</form>
```

Run the app with `dotnet run` and open `/Books` to try every action.

## Learn more

- [Get started with ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/tutorials/first-mvc-app/start-mvc)
- [Saving data with EF Core](https://learn.microsoft.com/ef/core/saving/)
