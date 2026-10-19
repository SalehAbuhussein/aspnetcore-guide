# EF Core performance

## Introduction

EF Core writes the SQL for you, which also means it can write slow SQL without you noticing. In most web apps the database is the slowest part of a request, so this is where speed problems usually live. This page shows how to see the SQL EF Core sends and how to fix the common mistakes. You need it once pages start to feel slow or the database gets busy. For a small app, the defaults from Part 1 are fine.

## Seeing the SQL

EF Core logs every command it runs under the `Microsoft.EntityFrameworkCore.Database.Command` category. Turn it on in development only.

```json [appsettings.Development.json]
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

Run the app and open a page. The console now shows each SQL statement and how long it took. To see the parameter values too, turn on sensitive data logging in development. Keep any options you already pass, such as the `UseSeeding` callbacks from [Seeding](/mvc/seeding), inside the same lambda.

```csharp [Program.cs (excerpt)]
builder.Services.AddDbContext<AppDbContext>(o =>
{
    o.UseSqlite(builder.Configuration.GetConnectionString("Default"));

    if (builder.Environment.IsDevelopment())
    {
        o.EnableSensitiveDataLogging();
    }
});
```

::: warning
Never turn on `EnableSensitiveDataLogging` in production. It writes user data, such as emails and password hashes, into your logs.
:::

You can also send logs straight to the console with `o.LogTo(Console.WriteLine, LogLevel.Information)`. The logging config above is better because it goes through the same [logging](/mvc/logging) setup as the rest of the app.

## The N+1 problem

N+1 means one query to load a list, then one more query for every item in it. Here the books page shows an average rating.

```csharp
// Slow: 1 query for books, then 1 per book.
var books = await db.Books.ToListAsync(ct);
var ratings = new Dictionary<int, double?>();

foreach (var book in books)
{
    ratings[book.Id] = await db.Reviews
        .Where(r => r.BookId == book.Id)
        .AverageAsync(r => (double?)r.Rating, ct);
}
```

These snippets run anywhere you have an `AppDbContext` named `db` and a `CancellationToken` named `ct`, such as a method in `BookService`. With 200 books that is 201 round trips to the database. The SQL log shows the same `SELECT` repeated with a different `BookId`. That repeated pattern is how you spot N+1.

Fix it with a projection. A projection uses `Select` to build exactly the shape you need, and EF Core turns it into one query.

```csharp
var books = await db.Books
    .OrderBy(b => b.Title)
    .Select(b => new BookListItem(
        b.Id,
        b.Title,
        b.Author,
        b.Reviews.Average(r => (double?)r.Rating)))
    .ToListAsync(ct);
```

```csharp [Models/BookListItem.cs]
namespace BookShelf.Models;

public record BookListItem(int Id, string Title, string Author, double? AverageRating);
```

When you need the full review objects, use `Include` instead: `db.Books.Include(b => b.Reviews)`. That loads books and their reviews in one query.

## AsNoTracking for reads

```csharp
var books = await db.Books.AsNoTracking().ToListAsync(ct);
```

By default EF Core tracks every entity it loads so it can detect changes on `SaveChangesAsync`. Tracking costs memory and time. For pages that only display data, `AsNoTracking` skips it. Projections into a record like `BookListItem` are never tracked, so they do not need it.

## Select only the columns you need

`db.Books.ToListAsync()` loads every column. If a book later gets a long `Description` column, the list page loads all of it for nothing. A projection with `Select` reads only the columns you name. Use projections for list pages, and load full entities only when you are about to edit them.

## AsSplitQuery for big includes

```csharp
var books = await db.Books
    .Include(b => b.Reviews)
    .AsSplitQuery()
    .ToListAsync(ct);
```

`Include` uses a SQL join, which repeats each book's columns once per review. With many reviews per book, that repeated data adds up. `AsSplitQuery` sends one query for books and a second for reviews. Use it when an include returns many child rows. For one book with a few reviews, the single query is faster.

## Bulk updates and deletes

Loading rows only to change one field and save them back wastes two trips. `ExecuteUpdateAsync` and `ExecuteDeleteAsync` run one SQL statement directly.

```csharp
await db.Books
    .Where(b => b.Year < 1900)
    .ExecuteUpdateAsync(s => s.SetProperty(b => b.Author, b => b.Author.Trim()), ct);

await db.Reviews
    .Where(r => r.Rating == 1 && r.Body == "")
    .ExecuteDeleteAsync(ct);
```

These skip the change tracker. Any entities already loaded in the same context do not see the change, and `SaveChangesAsync` is not needed.

## Indexes

An index lets the database find rows without scanning the whole table. Add one on columns you filter or sort by often.

```csharp [Models/Book.cs (excerpt)]
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Models;

[Index(nameof(Title))]
[Index(nameof(Author), nameof(Year))]
public class Book
{
    // properties as before
}
```

You can do the same in `OnModelCreating` with `modelBuilder.Entity<Book>().HasIndex(b => b.Title);`. Either way, add a [migration](/mvc/migrations) to create the index. EF Core already indexes foreign keys like `Review.BookId`.

## Compiled queries

```csharp [Services/BookService.cs (excerpt)]
private static readonly Func<AppDbContext, int, Task<Book?>> ById =
    EF.CompileAsyncQuery((AppDbContext db, int id) =>
        db.Books.FirstOrDefault(b => b.Id == id));

public Task<Book?> FindAsync(int id) => ById(db, id);
```

EF Core caches query translations already, so compiled queries save only a little. Use them for a handful of hot queries that run thousands of times a second, after measuring.

## Learn more

- [Efficient querying](https://learn.microsoft.com/ef/core/performance/efficient-querying)
- [Single vs. split queries](https://learn.microsoft.com/ef/core/querying/single-split-queries)
