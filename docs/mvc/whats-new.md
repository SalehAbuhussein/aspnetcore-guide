# What's new in .NET 10

## Introduction

This page lists the .NET 10 changes that matter when you build an MVC app such as `BookShelf`. Every item has a one-line summary, a short sample, and a link to the page in this guide that uses it.

.NET 10 is a long-term support (LTS) release. Microsoft supports it with fixes and security patches for three years from its November 2025 release, so it is a safe version to start a new app on.

## C# 14: the `field` keyword

You can add logic to a property setter without declaring a private backing field yourself. `field` refers to the one the compiler creates.

```csharp
public class Book
{
    public string Title
    {
        get;
        set => field = value.Trim();
    } = "";
}
```

See [Models](/mvc/models) for the `Book` model.

## C# 14: extension members

Extension blocks let you add extension properties, as well as methods, to a type you do not own.

```csharp
public static class StringExtensions
{
    extension(string s)
    {
        public bool IsBlank => string.IsNullOrWhiteSpace(s);
    }
}

// usage
if (search.IsBlank) { /* show all books */ }
```

See [Organizing](/mvc/organizing) for where helper classes like this belong.

## C# 14: null-conditional assignment

`?.` now works on the left side of `=`. The assignment runs only when the object is not null.

```csharp
book?.Title = "New title";
```

## EF Core 10: named query filters

A query filter adds a `WHERE` clause to every query on an entity. You can now give filters names and turn one off without turning off the others. This sample assumes you added a `bool IsDeleted` property to `Book`.

```csharp
modelBuilder.Entity<Book>()
    .HasQueryFilter("SoftDelete", b => !b.IsDeleted);

// later, in a query
var all = await db.Books.IgnoreQueryFilters(["SoftDelete"]).ToListAsync();
```

See [Querying](/mvc/querying).

## EF Core 10: `LeftJoin` in LINQ

LINQ has `LeftJoin` and `RightJoin` operators, and EF Core translates them to SQL `LEFT JOIN` and `RIGHT JOIN`. Before, you needed `GroupJoin` with `DefaultIfEmpty`.

```csharp
var rows = await db.Books
    .LeftJoin(db.Reviews,
        b => b.Id,
        r => r.BookId,
        (b, r) => new { b.Title, Rating = r == null ? (int?)null : r.Rating })
    .ToListAsync();
```

See [Relationships](/mvc/relationships) and [Querying](/mvc/querying).

## EF Core 10: `ExecuteUpdateAsync` with a normal lambda

The setters argument of `ExecuteUpdateAsync` takes a regular lambda now, so you can use `if` statements to decide which columns to set.

```csharp
await db.Books
    .Where(b => b.Id == id)
    .ExecuteUpdateAsync(s =>
    {
        s.SetProperty(b => b.Title, title);
        if (year is not null) s.SetProperty(b => b.Year, year.Value);
    });
```

See [EF Core performance](/mvc/ef-performance).

## SDK: `.slnx` is the default solution format

`dotnet new sln` creates an XML-based `.slnx` file instead of the old `.sln` format. It is shorter and easier to merge in Git.

```bash
dotnet new sln -n BookShelf
dotnet sln BookShelf.slnx add BookShelf/BookShelf.csproj
```

See [CLI cheat sheet](/mvc/cli).

## SDK: run a single `.cs` file

`dotnet run` can run one C# file with no project file. This is handy for quick experiments and small scripts.

```bash
dotnet run hello.cs
```

See [CLI cheat sheet](/mvc/cli).

## Learn more

- [Upgrade guide](/mvc/upgrade) to move an existing app to .NET 10.
- [What's new in ASP.NET Core in .NET 10](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0) on Microsoft Learn.
