# Your first API

## Introduction

This tutorial takes you from an empty folder to a working `/todos` API that stores data in SQLite, validates input, and describes itself with OpenAPI. Each step links to the page that explains it in depth, so you can build first and read later.

You need the .NET 10 SDK installed. See [Installation](/web-api/installation) if `dotnet --version` does not print a version that starts with 10.

<ApiStyle />

::: tip
The finished app is in `samples/TodoApi` in this guide's repository, so you can compare your code with it.
:::

## 1. Create the project

```bash
dotnet new webapi -n TodoApi
cd TodoApi
```

<div class="only-minimal">

`dotnet new webapi` creates a Minimal API project from a template. You get `Program.cs`, `appsettings.json`, a `TodoApi.http` file for sending test requests, and a reference to the OpenAPI package. [Project structure](/web-api/project-structure) walks through every file.

</div>

<div class="only-controllers">

`dotnet new webapi` creates a project from a template. You get `Program.cs`, `appsettings.json`, a `TodoApi.http` file for sending test requests, and a reference to the OpenAPI package. [Project structure](/web-api/project-structure) walks through every file.

The template starts in Minimal API style. Step 7 switches it to controllers. You could add `--use-controllers` to the command instead, but then you also get a sample `WeatherForecastController` to delete.

</div>

## 2. Clear out the sample code

Open `Program.cs` and replace its contents with this:

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
```

The template ships with a weather forecast endpoint. You do not need it. What remains creates the app, turns on the OpenAPI document in development, and starts the server.

## 3. Add the Todo model

```csharp [Models/Todo.cs]
namespace TodoApi.Models;

public class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
}
```

A model is a class that describes one kind of thing your API stores. EF Core will turn this class into a `Todos` table in the next step.

## 4. Connect a database

Install EF Core with the SQLite provider:

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
```

Create the `DbContext`, the class that represents your database session:

```csharp [Data/AppDbContext.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace TodoApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();
}
```

Add the connection string to `appsettings.json`:

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

Each `DbSet<T>` property is one table. The connection string tells SQLite which file to use. The [Database](/web-api/database) page explains each piece.

## 5. Add DTOs with validation rules

```csharp [Models/TodoDtos.cs]
using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models;

public record CreateTodo(
    [Required, StringLength(100)] string Title);

public class UpdateTodo
{
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = "";

    public bool IsDone { get; set; }
}
```

A DTO, or data transfer object, describes the shape of a request body. Clients send a `CreateTodo`, so they cannot set `Id` themselves. `[Required]` and `[StringLength(100)]` are validation rules. The [Validation](/web-api/validation) page lists more.

## 6. Write the endpoints

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs]
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Endpoints;

public static class TodoEndpoints
{
    public static void MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/todos");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
    }

    static async Task<Ok<List<Todo>>> GetAll(AppDbContext db)
    {
        var todos = await db.Todos.ToListAsync();
        return TypedResults.Ok(todos);
    }

    static async Task<Results<Ok<Todo>, NotFound>> GetById(int id, AppDbContext db)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(todo);
    }

    static async Task<Created<Todo>> Create(CreateTodo input, AppDbContext db)
    {
        var todo = new Todo { Title = input.Title };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/todos/{todo.Id}", todo);
    }

    static async Task<Results<NoContent, NotFound>> Update(int id, UpdateTodo input, AppDbContext db)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return TypedResults.NotFound();
        }

        todo.Title = input.Title;
        todo.IsDone = input.IsDone;
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    static async Task<Results<NoContent, NotFound>> Delete(int id, AppDbContext db)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return TypedResults.NotFound();
        }

        db.Todos.Remove(todo);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
```

`MapGroup("/todos")` gives all five endpoints the same URL prefix. Each handler asks for `AppDbContext` as a parameter and ASP.NET Core passes it in. `TypedResults` builds the response and its status code, such as `201 Created` or `404 Not Found`. [Building CRUD](/web-api/crud) goes through this file line by line, and [Minimal APIs](/web-api/minimal-apis) covers route groups.

</div>

<div class="only-controllers">

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
        return await db.Todos.ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Todo>> GetById(int id)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return NotFound();
        }

        return todo;
    }

    [HttpPost]
    public async Task<ActionResult<Todo>> Create(CreateTodo input)
    {
        var todo = new Todo { Title = input.Title };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTodo input)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return NotFound();
        }

        todo.Title = input.Title;
        todo.IsDone = input.IsDone;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return NotFound();
        }

        db.Todos.Remove(todo);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
```

`[ApiController]` turns on API behavior such as automatic validation. `[Route("todos")]` gives all five actions the same URL prefix, and `[HttpGet]`, `[HttpPost]`, and the other attributes pick the HTTP method for each one. The controller receives `AppDbContext` through its constructor. `NotFound()`, `NoContent()`, and `CreatedAtAction()` build the response and its status code, such as `201 Created` or `404 Not Found`. [Controllers](/web-api/controllers) explains each part.

</div>

## 7. Wire it up in Program.cs

<div class="only-minimal">

```csharp [Program.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddValidation();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapTodoEndpoints();

app.Run();
```

`AddDbContext` registers your context with [dependency injection](/web-api/dependency-injection), the built-in system that hands objects to your handlers. `AddValidation` checks every request against the rules from step 5 before your handler runs. `MapTodoEndpoints` adds the routes from step 6.

</div>

<div class="only-controllers">

```csharp [Program.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
```

`AddDbContext` registers your context with [dependency injection](/web-api/dependency-injection), the built-in system that hands objects to your controllers. `AddControllers` registers the services controllers need. You do not need `AddValidation`, because `[ApiController]` already checks every request against the rules from step 5 before your action runs. `MapControllers` adds the routes from step 6.

</div>

## 8. Create the database

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

A migration is a C# file that describes a schema change. The first command installs the EF Core command line tool. The second compares your model with an empty database and writes a migration that creates the `Todos` table. The third runs it and creates `todos.db`. See [Migrations](/web-api/migrations).

`dotnet ef` builds your app and reads the `AddDbContext` line from step 7 to find the database. That is why this step comes after `Program.cs` is wired up.

## 9. Test with TodoApi.http

Replace the template's `TodoApi.http` with this:

```http [TodoApi.http]
@TodoApi_HostAddress = http://localhost:5000

### List all todos
GET {{TodoApi_HostAddress}}/todos

### Get one todo
GET {{TodoApi_HostAddress}}/todos/1

### Create a todo
POST {{TodoApi_HostAddress}}/todos
Content-Type: application/json

{
  "title": "Write the CRUD page"
}

### Update a todo
PUT {{TodoApi_HostAddress}}/todos/1
Content-Type: application/json

{
  "title": "Write the CRUD page",
  "isDone": true
}

### Delete a todo
DELETE {{TodoApi_HostAddress}}/todos/1

### Break a validation rule
POST {{TodoApi_HostAddress}}/todos
Content-Type: application/json

{
  "title": ""
}
```

The template picks a random port for each new project. Change 5000 on the first line to the port of the `http` address in `Properties/launchSettings.json`. Start the app:

```bash
dotnet run
```

Send each request from Visual Studio, or from VS Code with the REST Client extension. Lines that start with `###` separate the requests.

1. `POST` returns `201 Created` with the new todo and its `id`.
2. `GET /todos` returns a list with that todo in it.
3. `PUT` returns `204 No Content`. A second `GET /todos/1` shows `"isDone": true`.
4. `DELETE` returns `204`, and `GET /todos/1` now returns `404`.
5. The empty title returns `400 Bad Request` with an `errors` object that names the `Title` field.

::: tip
Stop the app with Ctrl+C. Your todos are still there when you start it again, because they live in `todos.db`.
:::

## 10. Look at the OpenAPI document

With the app running, open `http://localhost:5000/openapi/v1.json` in your browser. Use the port from `Properties/launchSettings.json` in place of 5000. This JSON file lists every endpoint, its parameters, and each response it can send. ASP.NET Core builds it from your return types. The [OpenAPI](/web-api/openapi) page shows how to add Scalar, a web page where you can browse and call the endpoints.

## Where to go next

Your API has the same shape as the one the rest of this guide builds on. Good next pages:

- [Pagination](/web-api/pagination) to stop `GET /todos` returning every row.
- [Relationships](/web-api/relationships) to group todos into projects.
- [Authentication](/web-api/authentication) to know who is calling.
- [Testing](/web-api/testing) to check the endpoints automatically.

## Learn more

- [Tutorial: Create a minimal API with ASP.NET Core](https://learn.microsoft.com/aspnet/core/tutorials/min-web-api)
- [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview)
