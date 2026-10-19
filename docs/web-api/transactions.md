# Transactions and concurrency

## Introduction

Two problems show up once real users share your data. A multi-step change can fail halfway and leave the database inconsistent, and two people can edit the same todo at once so one silently overwrites the other. This page covers transactions for the first problem and optimistic concurrency for the second. A single-user prototype can skip both.

<ApiStyle />

## SaveChanges is already a transaction

```csharp
var project = new Project { Name = "Home" };
project.Todos.Add(new Todo { Title = "Fix the sink" });
project.Todos.Add(new Todo { Title = "Paint the fence" });

db.Projects.Add(project);
await db.SaveChangesAsync();
```

A transaction is a group of database changes that either all succeed or all roll back. EF Core wraps every `SaveChangesAsync` call in one. If the second insert fails, the project and the first todo are not saved either. For most endpoints, making all your changes and calling `SaveChangesAsync` once is all the transaction handling you need.

## Spanning several operations

You need an explicit transaction when one change happens outside `SaveChangesAsync`, such as `ExecuteUpdateAsync`, or when you call `SaveChangesAsync` more than once.

<div class="only-minimal">

```csharp [Endpoints/ProjectEndpoints.cs (excerpt)]
static async Task<Results<NoContent, NotFound>> Merge(int id, int targetId, AppDbContext db)
{
    var source = await db.Projects.FindAsync(id);
    if (source is null || !await db.Projects.AnyAsync(p => p.Id == targetId))
    {
        return TypedResults.NotFound();
    }

    await using var transaction = await db.Database.BeginTransactionAsync();

    await db.Todos
        .Where(t => t.ProjectId == id)
        .ExecuteUpdateAsync(s => s.SetProperty(t => t.ProjectId, targetId));

    db.Projects.Remove(source);
    await db.SaveChangesAsync();

    await transaction.CommitAsync();

    return TypedResults.NoContent();
}
```

Map it inside your projects group with `group.MapPost("/{id:int}/merge/{targetId:int}", Merge);`. If you do not have a projects endpoint file yet, create one the same way as `TodoEndpoints.cs`.

</div>

<div class="only-controllers">

```csharp [Controllers/ProjectsController.cs (excerpt)]
[HttpPost("{id:int}/merge/{targetId:int}")]
public async Task<IActionResult> Merge(int id, int targetId)
{
    var source = await db.Projects.FindAsync(id);
    if (source is null || !await db.Projects.AnyAsync(p => p.Id == targetId))
    {
        return NotFound();
    }

    await using var transaction = await db.Database.BeginTransactionAsync();

    await db.Todos
        .Where(t => t.ProjectId == id)
        .ExecuteUpdateAsync(s => s.SetProperty(t => t.ProjectId, targetId));

    db.Projects.Remove(source);
    await db.SaveChangesAsync();

    await transaction.CommitAsync();

    return NoContent();
}
```

The attribute maps it to `POST /projects/{id}/merge/{targetId}`. If you do not have a `ProjectsController` yet, create one the same way as `TodosController`, with `[Route("projects")]` and an `AppDbContext db` constructor parameter.

</div>

`BeginTransactionAsync` starts a transaction, and both the bulk update and `SaveChangesAsync` join it. `CommitAsync` makes the changes permanent. If an exception is thrown before that line, `await using` disposes the transaction, which rolls everything back.

## Optimistic concurrency

Alice and Bob both load todo 5. Alice marks it done. Bob renames it a minute later, and his `PUT` writes `IsDone = false` back, undoing Alice's change without anyone noticing. Optimistic concurrency detects this. Each row carries a version value, and an update only succeeds if the version is still the one the client read.

SQL Server has a `rowversion` type the database updates for you. SQLite has nothing like it, so you manage a `Guid` yourself.

```csharp [Models/Todo.cs (excerpt)]
// at the top of the file
using System.ComponentModel.DataAnnotations;

// in the Todo class
[ConcurrencyCheck]
public Guid Version { get; set; } = Guid.NewGuid();
```

```bash
dotnet ef migrations add AddTodoVersion
dotnet ef database update
```

`[ConcurrencyCheck]` tells EF Core to add `WHERE Version = @original` to every `UPDATE` and `DELETE` for this table. If no row matches, someone else changed it first, and EF Core throws `DbUpdateConcurrencyException`.

## Sending the version back

The client already gets `version` in every todo it reads. Add it to the update DTO so it can send it back.

```csharp [Models/TodoDtos.cs (excerpt)]
public class UpdateTodo
{
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = "";

    public bool IsDone { get; set; }

    public Guid Version { get; set; }
}
```

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Update(
    int id, UpdateTodo input, AppDbContext db)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return TypedResults.NotFound();
    }

    db.Entry(todo).Property(t => t.Version).OriginalValue = input.Version;

    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
    todo.Version = Guid.NewGuid();

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The todo was changed by someone else.",
            detail: "Reload the todo and apply your change again.");
    }

    return TypedResults.NoContent();
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpPut("{id:int}")]
public async Task<IActionResult> Update(int id, UpdateTodo input)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return NotFound();
    }

    db.Entry(todo).Property(t => t.Version).OriginalValue = input.Version;

    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
    todo.Version = Guid.NewGuid();

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The todo was changed by someone else.",
            detail: "Reload the todo and apply your change again.");
    }

    return NoContent();
}
```

</div>

The key line sets the original value of `Version` to what the client sent, not what `FindAsync` loaded. That makes the `WHERE` clause compare against the version the user saw on screen. Setting a new `Guid` on each update is what makes the next stale request fail.

<div class="only-minimal">

`TypedResults.Problem` returns a `409 Conflict` with a ProblemDetails body, the same error shape as on the [Errors](/web-api/errors) page. Clients should show the message and reload the todo, not retry blindly.

</div>

<div class="only-controllers">

`Problem` returns a `409 Conflict` with a ProblemDetails body, the same error shape as on the [Errors](/web-api/errors) page. Clients should show the message and reload the todo, not retry blindly.

</div>

::: tip
If you prefer HTTP headers, return the version as an `ETag` header on `GET` and read it back with `[FromHeader(Name = "If-Match")] string? ifMatch` on `PUT`. Parse it with `Guid.TryParse(ifMatch?.Trim('"'), out var version)`. The rest of the handler stays the same.
:::

## The SQL Server variant

```csharp [Models/Todo.cs (excerpt)]
[Timestamp]
public byte[] Version { get; set; } = [];
```

On SQL Server, `[Timestamp]` maps to a `rowversion` column. The database changes it on every write, so you drop the `todo.Version = Guid.NewGuid()` line. The DTO carries `byte[] Version`, which JSON sends as a base64 string. Everything else, including the `OriginalValue` line and the `409` response, is the same.

## Learn more

- [Handling concurrency conflicts](https://learn.microsoft.com/ef/core/saving/concurrency)
- [Using transactions](https://learn.microsoft.com/ef/core/saving/transactions)
