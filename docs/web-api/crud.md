# Building CRUD

## Introduction

CRUD stands for create, read, update, and delete, the four basic things an API does with data. This page builds all five `/todos` endpoints for `TodoApi`, stores the data with EF Core, and tests each one from a `.http` file.

You need the `Todo` model, `AppDbContext`, and an applied migration from the [Database](/web-api/database) and [Migrations](/web-api/migrations) pages before you start.

<ApiStyle />

## The endpoints you will build

| Method | Route | What it does | Success response |
| --- | --- | --- | --- |
| GET | `/todos` | List all todos | 200 OK |
| GET | `/todos/{id}` | Get one todo | 200 OK |
| POST | `/todos` | Create a todo | 201 Created |
| PUT | `/todos/{id}` | Replace a todo | 204 No Content |
| DELETE | `/todos/{id}` | Delete a todo | 204 No Content |

## Defining the DTOs

You already have these from [Requests](/web-api/requests) and [Validation](/web-api/validation).

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

A DTO, or data transfer object, is a small type that describes the shape of a request or response body. Using DTOs means a client cannot set fields you did not plan for, such as `Id`. A `record` is a short way to declare a class whose job is to hold data.

## Creating the endpoints

<div class="only-minimal">

This file replaces the in-memory version from [Minimal APIs](/web-api/minimal-apis).

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

</div>

<div class="only-controllers">

A controller is a class that groups related actions, the methods that handle requests. This file holds all five todo actions. If you started from the Minimal API template, it replaces `Endpoints/TodoEndpoints.cs`. You can also start fresh with `dotnet new webapi -n TodoApi --use-controllers`.

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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Todo>> GetById(int id)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return NotFound();
        }

        return Ok(todo);
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

</div>

The rest of this page walks through each part of this file.

## Routes and route constraints

<div class="only-minimal">

```csharp
var group = app.MapGroup("/todos");
group.MapGet("/{id:int}", GetById);
```

`MapGroup` creates a route group, a set of endpoints that share a URL prefix. The endpoint above answers `GET /todos/5`. The `:int` part is a route constraint, which means the route only matches when `id` is a whole number. See [Routing](/web-api/routing) for more constraints.

`MapTodoEndpoints` is an extension method, a static method that appears as if it belonged to another type. Here it adds a method to `IEndpointRouteBuilder`, so you can call `app.MapTodoEndpoints()`.

</div>

<div class="only-controllers">

```csharp
[Route("todos")]
public class TodosController(AppDbContext db) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Todo>> GetById(int id)
```

`[Route("todos")]` on the class sets a URL prefix for every action inside it. `[HttpGet("{id:int}")]` adds to that prefix, so this action answers `GET /todos/5`. The `:int` part is a route constraint, which means the route only matches when `id` is a whole number. See [Routing](/web-api/routing) for more constraints.

`ControllerBase` is the base class for API controllers. It gives you helper methods such as `Ok` and `NotFound`. `[ApiController]` turns on API behaviors: it reads complex parameters from the JSON body and returns `400` when validation fails. `AppDbContext` arrives through the primary constructor, and ASP.NET Core creates a new controller for every request.

</div>

## Return types

<div class="only-minimal">

```csharp
static async Task<Results<Ok<Todo>, NotFound>> GetById(int id, AppDbContext db)
```

`TypedResults` creates responses whose type includes the status code, such as `Ok<Todo>` or `NotFound`. `Results<Ok<Todo>, NotFound>` is a union type, meaning the method returns exactly one of the listed results. The compiler checks that you only return those types, and [OpenAPI](/web-api/openapi) reads them to document every possible response. See [Responses](/web-api/responses) for the full list.

</div>

<div class="only-controllers">

```csharp
public async Task<ActionResult<Todo>> GetById(int id)
```

`ActionResult<Todo>` means the action returns either a `Todo` or another result such as `NotFound()`. `Ok(todo)` sends `200` with the todo as JSON. [OpenAPI](/web-api/openapi) reads the `Todo` type to document the success response. Actions with no body to send back, such as `Update` and `Delete`, return `IActionResult`. See [Responses](/web-api/responses) for the full list.

</div>

## Reading todos

<div class="only-minimal">

`GetAll` loads every row with `ToListAsync` and wraps the list in `TypedResults.Ok`. If you already added [Pagination](/web-api/pagination), keep your paged `GetAll`. Paging and filters are covered there and on the [Querying](/web-api/querying) page.

</div>

<div class="only-controllers">

`GetAll` loads every row with `ToListAsync` and wraps the list in `Ok`. If you already added [Pagination](/web-api/pagination), keep your paged `GetAll`. Paging and filters are covered there and on the [Querying](/web-api/querying) page.

</div>

`GetById` uses `FindAsync`, which looks up a row by primary key and returns `null` when nothing matches. The method returns `404 Not Found` in that case.

## Creating a todo

<div class="only-minimal">

```csharp
return TypedResults.Created($"/todos/{todo.Id}", todo);
```

ASP.NET Core reads the JSON body into the `CreateTodo` parameter for you. After `SaveChangesAsync`, SQLite has assigned the new `Id`. `Created` sends a `201 Created` status with a `Location` header that points to the new resource, plus the todo in the body.

</div>

<div class="only-controllers">

```csharp
return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
```

ASP.NET Core reads the JSON body into the `CreateTodo` parameter for you. After `SaveChangesAsync`, SQLite has assigned the new `Id`. `CreatedAtAction` sends a `201 Created` status, plus the todo in the body. It builds the `Location` header from the `GetById` route and the new `id`, so the URL stays correct if you change the route later.

</div>

## Updating and deleting

`Update` and `Delete` follow the same pattern. Find the row, return `404` if it is missing, make the change, and save. Both return `204 No Content`, which means the request worked and there is no body to send back.

EF Core tracks the entity it loaded, so changing `todo.Title` and calling `SaveChangesAsync` is enough to issue an `UPDATE` statement.

## Wiring it up

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
// Keep the other lines you already have, such as AddOpenApi and UseHttpsRedirection.
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddValidation();

var app = builder.Build();

app.MapTodoEndpoints();

app.Run();
```

`Program.cs` stays short because all the todo routes live in `Endpoints/TodoEndpoints.cs`.

</div>

<div class="only-controllers">

```csharp [Program.cs (excerpt)]
// Keep the other lines you already have, such as AddOpenApi and UseHttpsRedirection.
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

`AddControllers` registers the services controllers need. `MapControllers` finds every controller class and maps the routes from its attributes, so `Program.cs` never changes when you add a controller.

</div>

## Testing with a .http file

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
```

A `.http` file holds HTTP requests that Visual Studio and VS Code (with the REST Client extension) can send with one click. Lines starting with `###` separate the requests. The template picks a random port, so use the port from `applicationUrl` in `Properties/launchSettings.json` in place of 5000 on the first line.

Run the app with `dotnet run`, then send the requests in order.

1. `POST` returns `201` with a `Location` header such as `/todos/3`.
2. `GET /todos/3` returns the new todo.
3. `PUT` returns `204`, and a second `GET` shows `"isDone": true`.
4. `DELETE` returns `204`, and a final `GET` returns `404`.

::: tip
JSON property names are camelCase by default, so `IsDone` in C# becomes `isDone` in JSON. Binding is not case sensitive, so either spelling works in request bodies.
:::

<div class="only-minimal">

Because `AddValidation` is on, a request with an empty title already returns `400 Bad Request` and never reaches the database. The [Validation](/web-api/validation) page explains the rules and the error response.

</div>

<div class="only-controllers">

Because the controller has `[ApiController]`, a request with an empty title already returns `400 Bad Request` with a ProblemDetails body and never reaches your action. It checks the data annotations on `CreateTodo` and `UpdateTodo` for you, so controllers do not need `AddValidation`. The [Validation](/web-api/validation) page explains the rules and the error response.

</div>

## Learn more

<div class="only-minimal">

- [Tutorial: Create a minimal API with ASP.NET Core](https://learn.microsoft.com/aspnet/core/tutorials/min-web-api)
- [Create responses in Minimal API applications](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses)

</div>

<div class="only-controllers">

- [Tutorial: Create a controller-based web API with ASP.NET Core](https://learn.microsoft.com/aspnet/core/tutorials/first-web-api)
- [Controller action return types in ASP.NET Core web API](https://learn.microsoft.com/aspnet/core/web-api/action-return-types)

</div>
