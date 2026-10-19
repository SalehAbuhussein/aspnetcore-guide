# Your first app

## Introduction

This tutorial builds BookShelf from an empty folder to a working book list. You will be able to add, edit, and delete books, and they will be saved in a SQLite database. Each step links to a page that explains the topic in more depth.

You need the .NET 10 SDK installed. See [Installation](/mvc/installation) if `dotnet --version` does not print a 10.x version.

::: tip
The finished app is in `samples/BookShelf` in this guide's repository, so you can compare your code with it.
:::

## Step 1: Create the project

```bash
dotnet new mvc -n BookShelf
cd BookShelf
dotnet run
```

`dotnet new mvc` creates a new MVC project from a template. `-n BookShelf` names the project and the folder. `dotnet run` builds and starts the app, and the terminal prints the address to open, such as `http://localhost:5123`. Open it and you will see the template's home page. Press `Ctrl+C` in the terminal to stop the app.

The [Project structure](/mvc/project-structure) page walks through each file the template created.

## Step 2: Add the Book model

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

A model is a plain C# class that holds data. `Id` will become the primary key, the column that identifies each row. See [Models](/mvc/models).

## Step 3: Install EF Core

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
```

EF Core is the library that reads and writes database rows as C# objects. The first package lets it talk to SQLite, a database that lives in a single file. The second package is needed by the migration tool in step 6. See [Database](/mvc/database).

## Step 4: Create the DbContext

```csharp [Data/AppDbContext.cs]
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
}
```

Create a `Data` folder and add this file. `AppDbContext` represents your database, and the `Books` property maps to a `Books` table.

## Step 5: Connect to the database

Add a `ConnectionStrings` section to `appsettings.json`.

```json [appsettings.json]
{
  "ConnectionStrings": {
    "Default": "Data Source=bookshelf.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Then register the context in `Program.cs`. Add the two `using` lines at the top and the `AddDbContext` call under `AddControllersWithViews`.

```csharp [Program.cs (excerpt)]
using BookShelf.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();
```

Leave the rest of the file as the template wrote it. `AddDbContext` tells ASP.NET Core how to build an `AppDbContext`, so controllers can ask for one. This is [dependency injection](/mvc/dependency-injection). The [Configuration](/mvc/configuration) page explains `appsettings.json`.

## Step 6: Create the database

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

The first command installs the EF Core command-line tool once for your user account. Skip it if `dotnet ef --version` already works. `migrations add` writes a C# file that describes the `Books` table. `database update` runs it, which creates `bookshelf.db` with the table inside. See [Migrations](/mvc/migrations).

## Step 7: Add the controller

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

A controller is a class whose public methods, called actions, handle requests. ASP.NET Core passes in an `AppDbContext` through the primary constructor. Each pair of GET and POST actions shows a form and then handles the submitted form. The [CRUD](/mvc/crud) page explains every action line by line, and [Controllers](/mvc/controllers) covers the basics.

## Step 8: Add the views

Create a `Views/Books` folder and add these five files. A view is a Razor file that mixes HTML with C# to build the page. See [Views](/mvc/views).

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

```razor [Views/Books/Delete.cshtml]
@model BookShelf.Models.Book

<h1>Delete @Model.Title?</h1>

<form asp-action="Delete" asp-route-id="@Model.Id" method="post">
    <button type="submit">Yes, delete it</button>
    <a asp-action="Index">Cancel</a>
</form>
```

Attributes that start with `asp-` are tag helpers. They build links and form fields from your controller and model, so `asp-for="Title"` writes the right `name` and `value` for the `Title` property. The [Forms](/mvc/forms) page covers them.

## Step 9: Link to the book list

Open `Views/Shared/_Layout.cshtml` and find the `<ul class="navbar-nav ...">` list. Add one more item after the Privacy link.

```razor [Views/Shared/_Layout.cshtml (excerpt)]
<li class="nav-item">
    <a class="nav-link text-dark" asp-controller="Books" asp-action="Index">Books</a>
</li>
```

The layout is the shared page frame around every view. Adding the link there puts it on every page. See [Layouts](/mvc/layouts).

## Step 10: Try it

```bash
dotnet run
```

Open the app, click Books, and add a book. It appears in the list, and it is still there after you stop and restart the app, because it lives in `bookshelf.db`. Then edit it and delete it to try the other actions.

::: tip
Right now the form accepts a blank title. Add rules on the [Validation](/mvc/validation) page next, then continue with [Relationships](/mvc/relationships) to give books reviews.
:::

## Learn more

- [Get started with ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/tutorials/first-mvc-app/start-mvc)
- [Getting started with EF Core](https://learn.microsoft.com/ef/core/get-started/overview/first-app)
