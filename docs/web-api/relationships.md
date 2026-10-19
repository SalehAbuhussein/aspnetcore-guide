# Relationships

## Introduction

Real data is connected. A project has many todos, and a todo can have many tags. This page adds a `Project` entity, links todos to it, and returns the related data from the API without running into JSON errors.

<ApiStyle />

## The Project entity

```csharp [Models/Project.cs]
namespace TodoApi.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Todo> Todos { get; set; } = [];
}
```

Add two properties to `Todo`:

```csharp [Models/Todo.cs (excerpt)]
public class Todo
{
    // Keep the properties you already have, such as DueDate.

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
}
```

This is a one-to-many relationship: one project, many todos. `ProjectId` is a foreign key, a column that stores the `Id` of the related row. It is nullable (`int?`), so a todo does not have to belong to a project. `Project` and `Todos` are navigation properties, which let you move from one object to the related ones in C#. EF Core finds the relationship from the names alone.

Register the new table:

```csharp [Data/AppDbContext.cs (excerpt)]
public DbSet<Todo> Todos => Set<Todo>();
public DbSet<Project> Projects => Set<Project>();
```

## The migration

```bash
dotnet ef migrations add AddProjects
dotnet ef database update
```

The migration creates a `Projects` table and adds a `ProjectId` column with a foreign key to `Todos`. Existing todos get `NULL` in that column. See [Migrations](/web-api/migrations).

## Why you return DTOs, not entities

Picture a project with one todo. The project's `Todos` list contains the todo. The todo's `Project` property points back to the project. When the JSON serializer writes the project, it writes the todo, then the todo's project, then that project's todos, and so on forever. This loop is called a cycle. System.Text.Json stops it by throwing a `JsonException` that says "A possible object cycle was detected".

The fix is to return response records that only contain the fields the client needs:

```csharp [Models/ProjectDtos.cs]
using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models;

public record CreateProject(
    [Required, StringLength(100)] string Name);

public record ProjectSummary(int Id, string Name, int TodoCount);

public record ProjectDetails(int Id, string Name, List<TodoItem> Todos);

public record TodoItem(int Id, string Title, bool IsDone);
```

`TodoItem` has no `Project` property, so there is nothing to loop back on. DTOs also stop you from leaking columns by accident when the entity grows.

You may see `ReferenceHandler.IgnoreCycles` suggested online. It hides the error but still sends entity shapes you did not design, so prefer DTOs.

## The project endpoints

<div class="only-minimal">

```csharp [Endpoints/ProjectEndpoints.cs]
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/projects");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create);
        group.MapGet("/{id:int}/todos", GetTodos);
        group.MapPost("/{id:int}/todos", AddTodo);
    }

    static async Task<Ok<List<ProjectSummary>>> GetAll(AppDbContext db)
    {
        var projects = await db.Projects
            .OrderBy(p => p.Name)
            .Select(p => new ProjectSummary(p.Id, p.Name, p.Todos.Count))
            .ToListAsync();

        return TypedResults.Ok(projects);
    }

    static async Task<Results<Ok<ProjectDetails>, NotFound>> GetById(int id, AppDbContext db)
    {
        var project = await db.Projects
            .Include(p => p.Todos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null)
        {
            return TypedResults.NotFound();
        }

        var todos = project.Todos
            .Select(t => new TodoItem(t.Id, t.Title, t.IsDone))
            .ToList();

        return TypedResults.Ok(new ProjectDetails(project.Id, project.Name, todos));
    }

    static async Task<Created<ProjectSummary>> Create(CreateProject input, AppDbContext db)
    {
        var project = new Project { Name = input.Name };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/projects/{project.Id}",
            new ProjectSummary(project.Id, project.Name, 0));
    }

    static async Task<Results<Ok<List<TodoItem>>, NotFound>> GetTodos(int id, AppDbContext db)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == id))
        {
            return TypedResults.NotFound();
        }

        var todos = await db.Todos
            .Where(t => t.ProjectId == id)
            .Select(t => new TodoItem(t.Id, t.Title, t.IsDone))
            .ToListAsync();

        return TypedResults.Ok(todos);
    }

    static async Task<Results<Created<TodoItem>, NotFound>> AddTodo(int id, CreateTodo input, AppDbContext db)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == id))
        {
            return TypedResults.NotFound();
        }

        var todo = new Todo { Title = input.Title, ProjectId = id };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/todos/{todo.Id}", new TodoItem(todo.Id, todo.Title, todo.IsDone));
    }
}
```

```csharp [Program.cs (excerpt)]
app.MapTodoEndpoints();
app.MapProjectEndpoints();
```

</div>

<div class="only-controllers">

```csharp [Controllers/ProjectsController.cs]
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Controllers;

[ApiController]
[Route("projects")]
public class ProjectsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProjectSummary>>> GetAll()
    {
        var projects = await db.Projects
            .OrderBy(p => p.Name)
            .Select(p => new ProjectSummary(p.Id, p.Name, p.Todos.Count))
            .ToListAsync();

        return Ok(projects);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDetails>> GetById(int id)
    {
        var project = await db.Projects
            .Include(p => p.Todos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null)
        {
            return NotFound();
        }

        var todos = project.Todos
            .Select(t => new TodoItem(t.Id, t.Title, t.IsDone))
            .ToList();

        return Ok(new ProjectDetails(project.Id, project.Name, todos));
    }

    [HttpPost]
    public async Task<ActionResult<ProjectSummary>> Create(CreateProject input)
    {
        var project = new Project { Name = input.Name };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = project.Id },
            new ProjectSummary(project.Id, project.Name, 0));
    }

    [HttpGet("{id:int}/todos")]
    public async Task<ActionResult<List<TodoItem>>> GetTodos(int id)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == id))
        {
            return NotFound();
        }

        var todos = await db.Todos
            .Where(t => t.ProjectId == id)
            .Select(t => new TodoItem(t.Id, t.Title, t.IsDone))
            .ToListAsync();

        return Ok(todos);
    }

    [HttpPost("{id:int}/todos")]
    public async Task<ActionResult<TodoItem>> AddTodo(int id, CreateTodo input)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == id))
        {
            return NotFound();
        }

        var todo = new Todo { Title = input.Title, ProjectId = id };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Created($"/todos/{todo.Id}", new TodoItem(todo.Id, todo.Title, todo.IsDone));
    }
}
```

`MapControllers` picks up the new controller on its own, so `Program.cs` does not change. `Create` uses `CreatedAtAction` to point at `GetById` in the same controller. `AddTodo` uses `Created` with a plain URL, because the new todo lives under `/todos`.

</div>

`AddTodo` sets `ProjectId` directly. You do not need to load the project to link a todo to it.

## Loading related data with Include

```csharp
var project = await db.Projects
    .Include(p => p.Todos)
    .FirstOrDefaultAsync(p => p.Id == id);
```

EF Core does not load navigation properties unless you ask. Without `Include`, `project.Todos` is an empty list. `Include` adds a join to the SQL so the todos come back in the same query.

`GetAll` and `GetTodos` skip `Include` and use `Select` instead. When you project into a record, EF Core reads `p.Todos.Count` straight from the database as a `COUNT`. That is the cheaper choice for lists. Use `Include` when you need the full related objects. The [Querying](/web-api/querying) page covers `Select` in more detail.

## Many-to-many: todos and tags

A todo can have several tags, and a tag can be on several todos. Give each side a list of the other:

```csharp [Models/Tag.cs]
namespace TodoApi.Models;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Todo> Todos { get; set; } = [];
}
```

```csharp [Models/Todo.cs (excerpt)]
public List<Tag> Tags { get; set; } = [];
```

Add `public DbSet<Tag> Tags => Set<Tag>();` to `AppDbContext` and create a migration. EF Core sees a list on both sides and creates a join table, `TagTodo`, with one row per link. You never touch that table. Add a tag with `todo.Tags.Add(tag)` and save, and load tags with `.Include(t => t.Tags)`. The same cycle rule applies, so return tag names in a DTO. Later pages in this guide do not use tags.

## Learn more

- [Relationships in EF Core](https://learn.microsoft.com/ef/core/modeling/relationships)
- [Eager loading of related data](https://learn.microsoft.com/ef/core/querying/related-data/eager)
