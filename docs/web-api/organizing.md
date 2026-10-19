# Organizing a larger app

## Introduction

With five endpoints, any layout works. With fifty, you start losing time looking for code and changing files in four folders for one feature. This page shows a layout that scales without adding layers you do not need. If your API has one or two features, keep the Part 1 layout and come back later.

<ApiStyle />

## One file per feature

<div class="only-minimal">

You already have `Endpoints/TodoEndpoints.cs`. Keep doing that: one `Endpoints/*Endpoints.cs` file per feature, each with one `Map...Endpoints` extension method.

```text
Endpoints/
  TodoEndpoints.cs
  ProjectEndpoints.cs
  HolidayEndpoints.cs
```

```csharp [Program.cs (excerpt)]
app.MapTodoEndpoints();
app.MapProjectEndpoints();
app.MapHolidayEndpoints();
```

</div>

<div class="only-controllers">

You already have `Controllers/TodosController.cs`. Keep doing that: one controller per feature, each with its own route.

```text
Controllers/
  TodosController.cs
  ProjectsController.cs
  HolidaysController.cs
```

```csharp [Program.cs (excerpt)]
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
```

`MapControllers` finds every controller class in the project, so a new controller needs no extra line in `Program.cs`.

</div>

This layout is fine for a long time. The pain starts when a feature has its HTTP code in one folder, DTOs in `Models/`, and logic in `Services/`, and every change touches all three folders.

## Feature folders

<div class="only-minimal">

```text
Features/
  Todos/
    TodoEndpoints.cs
    TodoDtos.cs
    TodoService.cs
    TodoFeature.cs
  Projects/
    ProjectEndpoints.cs
    ProjectDtos.cs
Data/
  AppDbContext.cs
Models/
  Todo.cs
  Project.cs
Program.cs
```

</div>

<div class="only-controllers">

```text
Features/
  Todos/
    TodosController.cs
    TodoDtos.cs
    TodoService.cs
    TodoFeature.cs
  Projects/
    ProjectsController.cs
    ProjectDtos.cs
Data/
  AppDbContext.cs
Models/
  Todo.cs
  Project.cs
Program.cs
```

</div>

A feature folder, sometimes called a vertical slice, keeps everything for one feature in one place. To change how todos work, you open `Features/Todos` and nothing else. Shared things, such as the entities and `AppDbContext`, stay in their own folders because several features use them.

Move the files and change their namespaces to `TodoApi.Features.Todos`. The code inside does not change, but files that use them need a matching `using` line.

<div class="only-minimal">

For example, `ProjectEndpoints.cs` uses `CreateTodo`, so it needs `using TodoApi.Features.Todos;`.

</div>

<div class="only-controllers">

For example, `ProjectsController.cs` uses `CreateTodo`, so it needs `using TodoApi.Features.Todos;`. Controllers keep working after the move, because `MapControllers` looks at every class in the project, whatever its folder or namespace.

</div>

## Keep Program.cs short with extension methods

Each feature registers its own services in one extension method:

```csharp [Features/Todos/TodoFeature.cs]
namespace TodoApi.Features.Todos;

public static class TodoFeature
{
    public static IServiceCollection AddTodoFeature(this IServiceCollection services)
    {
        services.AddScoped<TodoService>();
        return services;
    }
}
```

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
using TodoApi.Features.Projects;
using TodoApi.Features.Todos;

builder.Services.AddTodoFeature();

var app = builder.Build();

app.MapTodoEndpoints();
app.MapProjectEndpoints();
```

`Program.cs` now reads like a table of contents. Adding a feature means one `Add...` line and one `Map...` line.

</div>

<div class="only-controllers">

```csharp [Program.cs (excerpt)]
using TodoApi.Features.Todos;

builder.Services.AddControllers();
builder.Services.AddTodoFeature();

var app = builder.Build();

app.MapControllers();
```

`Program.cs` now reads like a table of contents. Adding a feature means one `Add...` line. `MapControllers` picks up the new controller on its own.

</div>

## Put logic in services

```csharp [Features/Todos/TodoService.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Features.Todos;

public class TodoService(AppDbContext db)
{
    public async Task<Todo?> CompleteAsync(int id, CancellationToken ct)
    {
        var todo = await db.Todos.FindAsync([id], ct);
        if (todo is null)
        {
            return null;
        }

        todo.IsDone = true;
        await db.SaveChangesAsync(ct);
        return todo;
    }
}
```

<div class="only-minimal">

```csharp [Features/Todos/TodoEndpoints.cs (excerpt)]
group.MapPost("/{id:int}/complete", Complete);

static async Task<Results<Ok<Todo>, NotFound>> Complete(
    int id, TodoService todos, CancellationToken ct)
{
    var todo = await todos.CompleteAsync(id, ct);
    return todo is null ? TypedResults.NotFound() : TypedResults.Ok(todo);
}
```

</div>

<div class="only-controllers">

```csharp [Features/Todos/TodosController.cs (excerpt)]
public class TodosController(AppDbContext db, TodoService todos) : ControllerBase
{
    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<Todo>> Complete(int id, CancellationToken ct)
    {
        var todo = await todos.CompleteAsync(id, ct);
        return todo is null ? NotFound() : Ok(todo);
    }
}
```

Add `TodoService` to the controller's constructor next to `AppDbContext`, and DI passes it in.

</div>

The handler deals with HTTP: reading input and choosing a status code. The service holds the rules. A handler that only reads a row with `AppDbContext` does not need a service. Add one when the logic grows past a few lines or when two endpoints need the same rule.

`TodoService` has no interface here. That is on purpose, see below.

## What not to add

Many .NET tutorials add layers that cost more than they give in an app this size. Skip these until you have the problem they solve.

### A generic repository over EF Core

`IRepository<T>` with `GetAll`, `GetById`, `Add` and `Delete` looks tidy. But `DbSet<T>` already is a repository, and `AppDbContext` already tracks changes. Wrapping it hides the useful parts, such as `Include`, `Select` and `AsNoTracking`, so you end up adding a method to the repository for every query. Use `AppDbContext` directly in services and handlers.

### A unit of work wrapper

`IUnitOfWork.Commit()` that calls `SaveChangesAsync` adds a type and does nothing new. `SaveChangesAsync` already saves all tracked changes in one transaction. For multi-step work, see [Transactions and concurrency](/web-api/transactions).

### An interface for every class

An interface earns its place when you have two implementations, or when you need to swap a dependency in tests, such as an email sender or an outside API client. An `ITodoService` with exactly one `TodoService` doubles the files and makes "go to definition" land on the interface. Register the class directly with `AddScoped<TodoService>()`. If you later need an interface, extracting one takes a minute.

### MediatR and CQRS

MediatR sends each request through a mediator object to a handler class. CQRS splits reads and writes into separate models. Both help in large systems with many teams or very different read and write needs. In a typical API they add a request class, a handler class and a registration for every endpoint, and your endpoint handler or controller action already is a handler. Note that MediatR also moved to a commercial license for larger companies. Add these only when you can name the problem they fix for you.

::: tip
A good test for any new layer: delete it in your head and ask what breaks. If the answer is "nothing, there would be fewer files," leave it out.
:::

## Learn more

- [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview)
- [Common web application architectures](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
