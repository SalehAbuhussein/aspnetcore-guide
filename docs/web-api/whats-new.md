# What's new in .NET 10

## Introduction

This page lists the .NET 10 changes that matter when you build an API such as `TodoApi`. Every item has a one-line summary, a short sample, and a link to the page in this guide that uses it.

.NET 10 is a long-term support (LTS) release. Microsoft supports it with fixes and security patches for three years from its November 2025 release, so it is a safe version to start a new API on.

## Built-in validation for Minimal APIs

Minimal APIs can now check data annotations such as `[Required]` and return a 400 with the errors, which before only controllers did. Call `AddValidation` once.

```csharp
builder.Services.AddValidation();

public record CreateTodo([Required, StringLength(100)] string Title);
```

Controllers already validated through `[ApiController]`, so nothing changes there. See [Validation](/web-api/validation).

## OpenAPI 3.1 by default

`AddOpenApi()` now produces an OpenAPI 3.1 document, and the built-in generator is based on version 2 of the `Microsoft.OpenApi` library. You can still ask for 3.0 if a tool needs it.

```csharp
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
});
```

See [OpenAPI](/web-api/openapi).

## API endpoints no longer redirect to a login page

When cookie authentication is on, a request to an API endpoint that is not signed in gets a `401` (or `403`) instead of a `302` redirect to `/Account/Login`. ASP.NET Core treats controllers with `[ApiController]` and Minimal API endpoints that read or write JSON as API endpoints. See [Authentication](/web-api/authentication).

## C# 14: the `field` keyword

You can add logic to a property setter without declaring a private backing field yourself. `field` refers to the one the compiler creates.

```csharp
public class Todo
{
    public int Id { get; set; }

    public string Title
    {
        get;
        set => field = value.Trim();
    } = "";
}
```

See [Database](/web-api/database) for the `Todo` entity.

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
if (search.IsBlank) return TypedResults.Ok(await db.Todos.ToListAsync());
```

See [Organizing](/web-api/organizing) for where helper classes like this belong.

## C# 14: null-conditional assignment

`?.` now works on the left side of `=`. The assignment runs only when the object is not null.

```csharp
todo?.Title = "New title";
```

## EF Core 10: named query filters

A query filter adds a `WHERE` clause to every query on an entity. You can now give filters names and turn one off without turning off the others. This sample assumes you added a `bool IsDeleted` property to `Todo`.

```csharp
modelBuilder.Entity<Todo>()
    .HasQueryFilter("SoftDelete", t => !t.IsDeleted);

// later, in a query
var all = await db.Todos.IgnoreQueryFilters(["SoftDelete"]).ToListAsync();
```

See [Querying](/web-api/querying).

## EF Core 10: `LeftJoin` in LINQ

LINQ has `LeftJoin` and `RightJoin` operators, and EF Core translates them to SQL `LEFT JOIN` and `RIGHT JOIN`. Before, you needed `GroupJoin` with `DefaultIfEmpty`.

```csharp
var rows = await db.Projects
    .LeftJoin(db.Todos,
        p => p.Id,
        t => t.ProjectId,
        (p, t) => new { p.Name, Title = t == null ? null : t.Title })
    .ToListAsync();
```

See [Relationships](/web-api/relationships) and [Querying](/web-api/querying).

## EF Core 10: `ExecuteUpdateAsync` with a normal lambda

The setters argument of `ExecuteUpdateAsync` takes a regular lambda now, so you can use `if` statements to decide which columns to set.

```csharp
await db.Todos
    .Where(t => t.Id == id)
    .ExecuteUpdateAsync(s =>
    {
        s.SetProperty(t => t.IsDone, true);
        if (title is not null) s.SetProperty(t => t.Title, title);
    });
```

See [EF Core performance](/web-api/ef-performance).

## SDK: `.slnx` is the default solution format

`dotnet new sln` creates an XML-based `.slnx` file instead of the old `.sln` format. It is shorter and easier to merge in Git.

```bash
dotnet new sln -n TodoApi
dotnet sln TodoApi.slnx add TodoApi/TodoApi.csproj
```

See [CLI cheat sheet](/web-api/cli).

## SDK: run a single `.cs` file

`dotnet run` can run one C# file with no project file. This is handy for quick experiments and small scripts.

```bash
dotnet run hello.cs
```

See [CLI cheat sheet](/web-api/cli).

## Learn more

- [Upgrade guide](/web-api/upgrade) to move an existing API to .NET 10.
- [What's new in ASP.NET Core in .NET 10](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0) on Microsoft Learn.
