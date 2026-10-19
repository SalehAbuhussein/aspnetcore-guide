# Database

## Introduction

Most APIs store data in a database. ASP.NET Core apps usually talk to a database through Entity Framework Core, often called EF Core. EF Core is an ORM, a library that maps C# classes to database tables so you work with objects instead of writing SQL by hand.

This page connects the `TodoApi` project to a SQLite database. SQLite stores the whole database in a single file, so there is no server to install.

<ApiStyle />

## Installing EF Core

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
```

The first package is the EF Core provider for SQLite. A provider is the part of EF Core that knows how to talk to one kind of database. The `Design` package adds the tools you need later to create [migrations](/web-api/migrations).

## The model

```csharp [Models/Todo.cs]
namespace TodoApi.Models;

public class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
}
```

EF Core turns this class into a `Todos` table. A property named `Id` becomes the primary key by convention, and SQLite fills in its value when you insert a row.

## Creating the DbContext

```csharp [Data/AppDbContext.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace TodoApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();
}
```

A `DbContext` is the class that represents your database session. Each `DbSet<T>` property represents one table, and you query or change that table through it. The constructor uses a primary constructor, a short C# syntax that declares constructor parameters right after the class name.

## Adding the connection string

```json [appsettings.json]
{
  "ConnectionStrings": {
    "Default": "Data Source=todos.db"
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

A connection string tells the provider where the database lives. For SQLite it is the path to the file. Keeping it in `appsettings.json` means you can change it per environment without touching code. See [Configuration](/web-api/configuration) for how settings are loaded.

## Registering the DbContext

```csharp [Program.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.Run();
```

`AddDbContext` registers `AppDbContext` with the [dependency injection](/web-api/dependency-injection) container. The container creates one context per request and disposes it when the request ends. `GetConnectionString("Default")` reads the value from the `ConnectionStrings` section.

## Using the DbContext in an endpoint

<div class="only-minimal">

```csharp [Program.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.MapGet("/todos", async (AppDbContext db) =>
    await db.Todos.ToListAsync());

app.Run();
```

Add `AppDbContext` as a parameter of the endpoint lambda and ASP.NET Core passes in the registered instance. You do not create the context yourself.

</div>

<div class="only-controllers">

```csharp [Program.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
```

```csharp [Controllers/TodosController.cs]
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Controllers;

[ApiController]
[Route("todos")]
public class TodosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Todo>>> GetAll()
    {
        var todos = await db.Todos.ToListAsync();
        return Ok(todos);
    }
}
```

Add `AppDbContext` as a constructor parameter of the controller and ASP.NET Core passes in the registered instance. You do not create the context yourself. `AddControllers` and `MapControllers` turn on controllers. The [CRUD](/web-api/crud) page builds out the full controller.

</div>

## Writing async queries

```csharp
var open = await db.Todos
    .Where(t => !t.IsDone)
    .OrderBy(t => t.Title)
    .ToListAsync();

var todo = await db.Todos.FindAsync(id);

db.Todos.Add(new Todo { Title = "Buy milk" });
await db.SaveChangesAsync();
```

Queries are written with LINQ, a set of C# methods such as `Where` and `OrderBy` that EF Core translates into SQL. Nothing runs against the database until you call a method like `ToListAsync`. Changes such as `Add` are tracked in memory and written when you call `SaveChangesAsync`.

::: tip
Use the `Async` versions of EF Core methods in web apps. While the database works, the thread is free to serve other requests.
:::

The database file and the `Todos` table do not exist yet. The next page, [Migrations](/web-api/migrations), creates them.

## Learn more

- [Entity Framework Core overview](https://learn.microsoft.com/ef/core/)

<div class="only-minimal">

- [Tutorial: Create a minimal API with ASP.NET Core](https://learn.microsoft.com/aspnet/core/tutorials/min-web-api)

</div>

<div class="only-controllers">

- [Tutorial: Create a controller-based web API with ASP.NET Core](https://learn.microsoft.com/aspnet/core/tutorials/first-web-api)

</div>
