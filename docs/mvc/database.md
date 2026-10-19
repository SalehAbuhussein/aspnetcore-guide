# Database

## Introduction

Most web apps store data in a database. ASP.NET Core apps usually talk to the database through Entity Framework Core, often called EF Core. This page connects BookShelf to a SQLite database and reads books from it.

## What EF Core is

EF Core is an ORM. An ORM (object-relational mapper) is a library that turns rows in database tables into C# objects and back again. You write C# classes and LINQ queries, and EF Core writes the SQL for you.

SQLite is a small database engine that stores everything in one file. It needs no server, which makes it a good fit for learning. You can switch to SQL Server or PostgreSQL later by changing the provider package and one line of setup.

## Installing the packages

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
```

Run these commands in the folder that holds `BookShelf.csproj`. The first package is the SQLite provider, the part of EF Core that knows how to talk to SQLite. The second package contains design-time code that the migration tools need, which you will use on the [Migrations](/mvc/migrations) page.

## Creating the DbContext

```csharp [Data/AppDbContext.cs]
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
}
```

A `DbContext` is the class that represents one working session with the database. Each `DbSet<T>` property maps to a table, so `Books` maps to a `Books` table with one column per property on the `Book` [model](/mvc/models). EF Core treats the `Id` property as the primary key by convention.

## Adding the connection string

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
  "AllowedHosts": "*",
  "Shelf": {
    "Name": "My BookShelf",
    "PageSize": 10
  }
}
```

A connection string tells the provider where the database lives. For SQLite it is a file path, so this one creates `bookshelf.db` in the project folder. See [Configuration](/mvc/configuration) for how settings files work.

## Registering the DbContext

```csharp [Program.cs (excerpt)]
using BookShelf.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();
```

Keep the lines you added on earlier pages, such as `Configure<ShelfOptions>` and `AddSession`. `AddDbContext` registers `AppDbContext` with the [dependency injection](/mvc/dependency-injection) container. `GetConnectionString("Default")` reads the value from the `ConnectionStrings` section. ASP.NET Core now creates one `AppDbContext` per request and gives it to any controller that asks for it.

## Querying with LINQ

```csharp [Controllers/BooksController.cs]
using BookShelf.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

public class BooksController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var books = await db.Books
            .Where(b => b.Year >= 2000)
            .OrderBy(b => b.Title)
            .ToListAsync();

        return View(books);
    }

    public async Task<IActionResult> Details(int id)
    {
        var book = await db.Books.FindAsync(id);

        if (book is null)
        {
            return NotFound();
        }

        return View(book);
    }
}
```

LINQ (Language Integrated Query) is a set of C# methods like `Where` and `OrderBy` for filtering and sorting data. EF Core does not run the query until you ask for results. `ToListAsync` sends the SQL and returns a `List<Book>`.

`FindAsync` looks up a row by primary key and returns `null` when nothing matches. Other useful methods are `FirstOrDefaultAsync`, `CountAsync`, and `AnyAsync`.

::: tip
Use the `Async` versions of query methods with `await`. While the database works, the server thread is free to handle other requests.
:::

The database file and the `Books` table do not exist yet. The [Migrations](/mvc/migrations) page shows how to create them.

## Learn more

- [Entity Framework Core overview](https://learn.microsoft.com/ef/core/)
- [Querying data with EF Core](https://learn.microsoft.com/ef/core/querying/)
