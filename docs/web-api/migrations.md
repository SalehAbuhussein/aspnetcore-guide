# Migrations

## Introduction

A migration is a small C# file that describes a change to your database schema, such as creating a table or adding a column. EF Core writes migrations for you by comparing your model classes with the last known schema. You keep them in source control so every developer and every server can build the same database.

## Installing the EF Core tools

```bash
dotnet tool install --global dotnet-ef
```

This installs `dotnet-ef`, a command line tool that creates and applies migrations. A global tool is installed once per machine and works in any project. If you already have it, run `dotnet tool update --global dotnet-ef` to get the latest version.

Your project also needs the `Microsoft.EntityFrameworkCore.Design` package, which you added on the [Database](/web-api/database) page.

## Creating your first migration

```bash
dotnet ef migrations add InitialCreate
```

EF Core builds your project, reads `AppDbContext`, and creates a `Migrations` folder. Inside you find a file named with a timestamp and `InitialCreate`, plus a snapshot file that records what the model looks like now. Open the migration file to see the `Up` method that creates the `Todos` table and the `Down` method that drops it.

## Applying migrations

```bash
dotnet ef database update
```

This runs every migration that the database has not seen yet. For SQLite, the command creates `todos.db` if it does not exist. EF Core records applied migrations in a table named `__EFMigrationsHistory`, so running the command twice does nothing the second time.

## Adding a column

Add a property to the model.

```csharp [Models/Todo.cs]
namespace TodoApi.Models;

public class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
    public DateTime? DueDate { get; set; }
}
```

Then create and apply a new migration.

```bash
dotnet ef migrations add AddDueDate
dotnet ef database update
```

EF Core compares the model with the snapshot and sees one new property. The new migration adds a nullable `DueDate` column. The `?` after `DateTime` makes the property nullable, so existing rows can keep an empty value.

::: tip
Give each migration a name that describes the change, like `AddDueDate`. The names show up in the history table and make problems easier to track down.
:::

## Removing a migration

```bash
dotnet ef migrations remove
```

This deletes the most recent migration and restores the snapshot. Use it when you made a mistake and have not applied the migration yet. If you already applied it, first roll the database back to the previous migration.

```bash
dotnet ef database update InitialCreate
dotnet ef migrations remove
```

::: warning
Do not remove a migration that has already run on a shared or production database. Add a new migration that reverses the change instead.
:::

## Seeding data

```csharp [Data/AppDbContext.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace TodoApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Todo>().HasData(
            new Todo { Id = 1, Title = "Read the docs", IsDone = false },
            new Todo { Id = 2, Title = "Build an API", IsDone = false });
    }
}
```

`HasData` tells EF Core to insert these rows as part of a migration. Seeding means filling the database with starting data. You must set `Id` yourself because EF Core uses it to track the rows between migrations. After changing the seed data, add a migration and run `dotnet ef database update` again.

HasData works best for small, fixed data such as lookup values. For test data that changes often, insert rows from code at startup instead.

## Learn more

- [Migrations overview](https://learn.microsoft.com/ef/core/managing-schemas/migrations/)
- [Data seeding](https://learn.microsoft.com/ef/core/modeling/data-seeding)
