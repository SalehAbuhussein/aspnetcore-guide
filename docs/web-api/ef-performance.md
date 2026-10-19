# EF Core performance

## Introduction

EF Core writes SQL for you, which means it can also write slow SQL for you without you noticing. This page shows how to see the SQL, spot the common mistakes, and fix them. Come here when an endpoint gets slow as data grows. With a few hundred rows, almost any query is fast, so do not tune before you measure.

<ApiStyle />

## See the SQL

```json [appsettings.Development.json]
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

Every query now appears in the console with its SQL and how long it took. Keep this on in Development. Parameter values are hidden by default. To see them, add `options.EnableSensitiveDataLogging()` inside `AddDbContext`, in Development only, because it can log personal data.

## The N+1 problem

```csharp
// Slow: 1 query for projects, then 1 query per project
var projects = await db.Projects.ToListAsync(ct);
var result = new List<OpenTodoSummary>();

foreach (var project in projects)
{
    var open = await db.Todos.CountAsync(t => t.ProjectId == project.Id && !t.IsDone, ct);
    result.Add(new OpenTodoSummary(project.Id, project.Name, open));
}
```

With 200 projects this sends 201 queries. The log shows the same `SELECT COUNT(*)` again and again, which is how you spot it. Each query is fast on its own, but the round trips add up.

Fix it with a projection, a `Select` that tells EF Core exactly what you want:

```csharp [Models/OpenTodoSummary.cs]
namespace TodoApi.Models;

public record OpenTodoSummary(int Id, string Name, int OpenTodos);
```

<div class="only-minimal">

```csharp [Endpoints/ProjectEndpoints.cs (excerpt)]
static async Task<Ok<List<OpenTodoSummary>>> GetSummaries(AppDbContext db, CancellationToken ct)
{
    var summaries = await db.Projects
        .Select(p => new OpenTodoSummary(
            p.Id,
            p.Name,
            p.Todos.Count(t => !t.IsDone)))
        .ToListAsync(ct);

    return TypedResults.Ok(summaries);
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/ProjectsController.cs (excerpt)]
[HttpGet("summaries")]
public async Task<ActionResult<List<OpenTodoSummary>>> GetSummaries(CancellationToken ct)
{
    var summaries = await db.Projects
        .Select(p => new OpenTodoSummary(
            p.Id,
            p.Name,
            p.Todos.Count(t => !t.IsDone)))
        .ToListAsync(ct);

    return Ok(summaries);
}
```

</div>

EF Core turns this into one SQL query with a subquery for the count. One round trip, no matter how many projects.

## Select into DTOs

Projection helps beyond N+1. Any read endpoint should load only the columns it returns:

```csharp
var todos = await db.Todos
    .Where(t => t.ProjectId == projectId)
    .Select(t => new TodoListItem(t.Id, t.Title, t.IsDone))
    .ToListAsync(ct);
```

Here `TodoListItem` is a small record like `OpenTodoSummary`. Projected results are never tracked, the SQL selects fewer columns, and you avoid the serialization cycles you get from returning entities with navigation properties.

## AsNoTracking

```csharp
var todos = await db.Todos.AsNoTracking().ToListAsync(ct);
```

By default, EF Core tracks every entity it loads so it can detect changes on `SaveChangesAsync`. That costs memory and time. `AsNoTracking` skips it. Use it for every query that returns full entities and does not modify them. Do not use it when you load an entity to update it, because `SaveChangesAsync` will not see the change.

## AsSplitQuery

```csharp
var projects = await db.Projects
    .Include(p => p.Todos)
    .AsSplitQuery()
    .AsNoTracking()
    .ToListAsync(ct);
```

`Include` loads related rows with a `JOIN`. With a big collection, the join repeats every project column on every todo row. `AsSplitQuery` sends two queries instead, one for projects and one for their todos. Use it when you include one or more large collections. A projection with `Select` often avoids the need for `Include` at all.

## Bulk updates and deletes

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
group.MapPost("/complete", CompleteProject);
group.MapDelete("/done", DeleteDone);

static async Task<Ok<int>> CompleteProject(int projectId, AppDbContext db, CancellationToken ct)
{
    var updated = await db.Todos
        .Where(t => t.ProjectId == projectId && !t.IsDone)
        .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDone, true), ct);

    return TypedResults.Ok(updated);
}

static async Task<NoContent> DeleteDone(AppDbContext db, CancellationToken ct)
{
    await db.Todos.Where(t => t.IsDone).ExecuteDeleteAsync(ct);
    return TypedResults.NoContent();
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpPost("complete")]
public async Task<ActionResult<int>> CompleteProject([FromQuery] int projectId, CancellationToken ct)
{
    var updated = await db.Todos
        .Where(t => t.ProjectId == projectId && !t.IsDone)
        .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDone, true), ct);

    return Ok(updated);
}

[HttpDelete("done")]
public async Task<IActionResult> DeleteDone(CancellationToken ct)
{
    await db.Todos.Where(t => t.IsDone).ExecuteDeleteAsync(ct);
    return NoContent();
}
```

</div>

`POST /todos/complete?projectId=3` marks every open todo in project 3 as done with one `UPDATE` statement. Without `ExecuteUpdateAsync`, you would load every todo, change it in memory, and save, which means one query to read plus one `UPDATE` per row. Both methods return the number of rows changed.

These methods run right away and bypass the change tracker. Entities you already loaded in the same context do not see the change, and `SaveChangesAsync` is not involved.

## Indexes

```csharp [Data/AppDbContext.cs (excerpt)]
// inside OnModelCreating, after the HasData call
modelBuilder.Entity<Todo>()
    .HasIndex(t => new { t.ProjectId, t.IsDone });
```

An index lets the database find rows without scanning the whole table. Add one for columns you filter or sort by often. EF Core already creates an index for every foreign key, such as `ProjectId`. The composite index above helps the "open todos in a project" query. Create a [migration](/web-api/migrations) after adding it.

## Compiled queries

```csharp
private static readonly Func<AppDbContext, int, CancellationToken, Task<Todo?>> GetTodo =
    EF.CompileAsyncQuery((AppDbContext db, int id, CancellationToken ct) =>
        db.Todos.FirstOrDefault(t => t.Id == id));

var todo = await GetTodo(db, id, ct);
```

EF Core caches query translations already, so most apps never need this. A compiled query skips the cache lookup too. Consider it only for a very hot query after a profiler shows translation time matters.

::: tip
Measure first. Turn on SQL logging, find the endpoint that is slow, and look at its queries. Most fixes are a missing projection, a missing index, or an N+1 loop.
:::

## Learn more

- [Efficient querying](https://learn.microsoft.com/ef/core/performance/efficient-querying)
- [ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete)
