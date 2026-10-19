# Seeding

## Introduction

Seeding means putting starting data into the database. EF Core has two ways to do it: `HasData` for fixed rows that every database needs, and `UseSeeding` for sample data you add when the app or the tools create the database. This page shows both and when to use each.

## HasData for fixed data

```csharp [Data/AppDbContext.cs]
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>().HasData(
            new Book { Id = 1, Title = "The Hobbit", Author = "J.R.R. Tolkien", Year = 1937 },
            new Book { Id = 2, Title = "Dune", Author = "Frank Herbert", Year = 1965 });
    }
}
```

```bash
dotnet ef migrations add SeedBooks
dotnet ef database update
```

`HasData` is part of the model, so the rows go into a migration as `INSERT` statements. You must set `Id` yourself, because EF Core compares ids to see which rows changed. If you edit a seed row later, the next migration writes an `UPDATE`.

Use `HasData` for small lookup data that never changes at runtime, such as a list of genres or countries. It is a poor fit for large or random sample data, because every change produces a new migration. If you started with the `HasData` call on the [Migrations](/mvc/migrations) page, you already have this.

## UseSeeding for sample data

EF Core 9 added `UseSeeding` and `UseAsyncSeeding`. You set them where you register the context, and EF Core runs them whenever it creates or migrates the database.

```csharp [Program.cs (excerpt)]
using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<AppDbContext>(o => o
    .UseSqlite(builder.Configuration.GetConnectionString("Default"))
    .UseSeeding((context, _) =>
    {
        if (!context.Set<Book>().Any(b => b.Title == "Project Hail Mary"))
        {
            context.Set<Book>().Add(SampleData.Book());
            context.SaveChanges();
        }
    })
    .UseAsyncSeeding(async (context, _, ct) =>
    {
        if (!await context.Set<Book>().AnyAsync(b => b.Title == "Project Hail Mary", ct))
        {
            context.Set<Book>().Add(SampleData.Book());
            await context.SaveChangesAsync(ct);
        }
    }));
```

```csharp [Data/SampleData.cs]
using BookShelf.Models;

namespace BookShelf.Data;

public static class SampleData
{
    public static Book Book() => new()
    {
        Title = "Project Hail Mary",
        Author = "Andy Weir",
        Year = 2021,
        Reviews =
        [
            new Review { Rating = 5, Body = "Could not put it down." },
            new Review { Rating = 4, Body = "Great science, fun narrator." }
        ]
    };
}
```

The callback receives the `DbContext` as its base type, so you reach tables with `context.Set<Book>()`. The second argument tells you whether EF Core created the database in this run, which you can ignore here. The book is added with its reviews in one `SaveChanges`, and EF Core fills in the ids and `BookId` for you.

Write both versions. `dotnet ef database update` calls the sync `UseSeeding`, and `MigrateAsync` in your app calls `UseAsyncSeeding`.

::: warning
Seeding code runs every time the database is migrated, not once. Always check whether the data is already there before you add it, as the `Any` checks above do. Otherwise each run adds another copy.
:::

## Seeding only in development

Sample data belongs on your machine, not in production. Apply migrations at startup in development only, which also triggers the async seed.

```csharp [Program.cs (excerpt)]
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

`CreateScope` makes a short-lived scope, because `AppDbContext` is a scoped service and there is no request yet at startup. `MigrateAsync` applies any pending migrations and then runs your `UseAsyncSeeding` callback. In production, apply migrations as a deploy step instead. See [Deployment](/mvc/deployment).

`dotnet ef database update` still runs the seed against whatever database you point it at. To keep the callbacks out of production too, give the `AddDbContext` lambda a block body, call `UseSqlite` first, and then call `UseSeeding` and `UseAsyncSeeding` inside `if (builder.Environment.IsDevelopment())`.

## Re-running the seed

Because the seed checks before it inserts, you can run it as often as you like. To start over with a clean database, drop it and migrate again.

```bash
dotnet ef database drop --force
dotnet ef database update
```

`database drop` deletes `bookshelf.db`. `database update` recreates the tables, inserts the `HasData` rows from the migrations, and then runs `UseSeeding`. Running the app in development does the same through `MigrateAsync`.

## Learn more

- [Data seeding](https://learn.microsoft.com/ef/core/modeling/data-seeding)
- [Applying migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying)
