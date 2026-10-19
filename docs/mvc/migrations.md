# Migrations

## Introduction

A migration is a small C# file that describes a change to your database schema, such as creating a table or adding a column. EF Core generates migrations from your model classes, so the database always matches your code. This page assumes you have set up `AppDbContext` from the [Database](/mvc/database) page.

## Installing the dotnet-ef tool

```bash
dotnet tool install --global dotnet-ef
```

`dotnet-ef` is a command-line tool that creates and applies migrations. The `--global` flag installs it once for your user account, so it works in every project. Check that it installed with `dotnet ef --version`.

The tool also needs the `Microsoft.EntityFrameworkCore.Design` package in your project. You added it on the [Database](/mvc/database) page.

## Creating your first migration

```bash
dotnet ef migrations add InitialCreate
```

This command compares your `AppDbContext` and `Book` model with the last known schema and writes the difference to a new `Migrations` folder. `InitialCreate` is the name you give the migration. Pick a name that describes the change.

Open `Migrations/<timestamp>_InitialCreate.cs` and you will see an `Up` method that creates the `Books` table and a `Down` method that drops it. You can read and edit these files, but you rarely need to.

## Applying migrations

```bash
dotnet ef database update
```

This command runs every migration that has not been applied yet. For SQLite it creates `bookshelf.db` if the file does not exist. EF Core records applied migrations in a table named `__EFMigrationsHistory`, so it never runs the same migration twice.

## Adding a column

Add a property to the model. The validation attributes from the [Validation](/mvc/validation) page stay.

```csharp [Models/Book.cs]
using System.ComponentModel.DataAnnotations;

namespace BookShelf.Models;

public class Book
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string Author { get; set; } = "";

    [Range(1450, 2100, ErrorMessage = "Enter a year between 1450 and 2100.")]
    public int Year { get; set; }

    public string? Isbn { get; set; }
}
```

Then create and apply a new migration.

```bash
dotnet ef migrations add AddIsbnToBook
dotnet ef database update
```

EF Core sees the new `Isbn` property and generates a migration that adds an `Isbn` column to `Books`. The `?` makes the column nullable, which means existing rows can have no value.

## Removing the last migration

```bash
dotnet ef migrations remove
```

This deletes the most recent migration file, as long as it has not been applied to the database. Use it when you notice a mistake right after running `migrations add`.

::: warning
If you already ran `database update`, roll the database back first with `dotnet ef database update <PreviousMigrationName>`, then run `migrations remove`. Do not delete migration files by hand.
:::

## Seeding data

Seeding means filling the database with starting data. Override `OnModelCreating` in your context and call `HasData`.

```csharp [Data/AppDbContext.cs]
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();

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

`HasData` becomes part of the model, so you add a migration to insert the rows. You must set `Id` yourself, because EF Core uses it to track which seed rows exist.

## Learn more

- [Migrations overview](https://learn.microsoft.com/ef/core/managing-schemas/migrations/)
- [EF Core tools reference (.NET CLI)](https://learn.microsoft.com/ef/core/cli/dotnet)
