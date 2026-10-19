# Querying

## Introduction

Clients rarely want every todo. This page adds filters such as `?isDone=true&search=milk`, sorting with `?sort=title`, and response records that only carry the columns you need.

<ApiStyle />

## The query record

```csharp [Models/TodoQuery.cs]
namespace TodoApi.Models;

public record TodoQuery(bool? IsDone = null, string? Search = null, string? Sort = null);

public record TodoResponse(int Id, string Title, bool IsDone);

public record TodoStats(int Total, int Done, int Open);
```

Every filter is nullable, written with `?`. A `null` value means the client did not send that parameter, so you skip that filter. `TodoResponse` is the shape you send back. `TodoStats` is used further down.

## Building the query step by step

<div class="only-minimal">

Replace `GetAll` in `Endpoints/TodoEndpoints.cs`. This version replaces the one from [CRUD](/web-api/crud). If you added paging, the last section below puts it back:

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
static async Task<Ok<List<TodoResponse>>> GetAll([AsParameters] TodoQuery query, AppDbContext db)
{
    IQueryable<Todo> todos = db.Todos;

    if (query.IsDone is not null)
    {
        todos = todos.Where(t => t.IsDone == query.IsDone);
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
        todos = todos.Where(t => EF.Functions.Like(t.Title, $"%{query.Search}%"));
    }

    todos = query.Sort switch
    {
        "title" => todos.OrderBy(t => t.Title).ThenBy(t => t.Id),
        "-title" => todos.OrderByDescending(t => t.Title).ThenBy(t => t.Id),
        _ => todos.OrderBy(t => t.Id)
    };

    var result = await todos
        .Select(t => new TodoResponse(t.Id, t.Title, t.IsDone))
        .ToListAsync();

    return TypedResults.Ok(result);
}
```

`IQueryable<Todo>` is a query that has not run yet. Each `Where` adds a condition to it, but nothing goes to the database until `ToListAsync`. That lets you add filters with plain `if` statements and still send one SQL query. `[AsParameters]` fills `TodoQuery` from the query string, so `?isDone=true` sets `IsDone`.

</div>

<div class="only-controllers">

Replace `GetAll` in `Controllers/TodosController.cs`. This version replaces the one from [CRUD](/web-api/crud). If you added paging, the last section below puts it back:

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet]
public async Task<ActionResult<List<TodoResponse>>> GetAll([FromQuery] TodoQuery query)
{
    IQueryable<Todo> todos = db.Todos;

    if (query.IsDone is not null)
    {
        todos = todos.Where(t => t.IsDone == query.IsDone);
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
        todos = todos.Where(t => EF.Functions.Like(t.Title, $"%{query.Search}%"));
    }

    todos = query.Sort switch
    {
        "title" => todos.OrderBy(t => t.Title).ThenBy(t => t.Id),
        "-title" => todos.OrderByDescending(t => t.Title).ThenBy(t => t.Id),
        _ => todos.OrderBy(t => t.Id)
    };

    var result = await todos
        .Select(t => new TodoResponse(t.Id, t.Title, t.IsDone))
        .ToListAsync();

    return Ok(result);
}
```

`IQueryable<Todo>` is a query that has not run yet. Each `Where` adds a condition to it, but nothing goes to the database until `ToListAsync`. That lets you add filters with plain `if` statements and still send one SQL query. `[FromQuery]` fills `TodoQuery` from the query string, so `?isDone=true` sets `IsDone`. You need it here because `[ApiController]` reads a complex type such as `TodoQuery` from the JSON body by default.

</div>

## Searching text

```csharp
todos = todos.Where(t => EF.Functions.Like(t.Title, $"%{query.Search}%"));
```

`EF.Functions.Like` becomes a SQL `LIKE`, where `%` matches any run of characters. In SQLite, `LIKE` ignores case for English letters, so `search=milk` finds "Buy Milk". `t.Title.Contains(query.Search)` also works, but on SQLite it is case sensitive.

The search text is sent as a SQL parameter, not pasted into the SQL string, so it is safe from SQL injection.

## Sorting

The `switch` expression picks an order based on the `sort` value. A leading `-` means descending, a common convention in APIs. Anything else falls back to sorting by `Id`, so a typo never breaks the request. Keep the list of allowed sort fields in code like this. Do not build `OrderBy` from a column name the client sends.

## Projecting with Select

```csharp
.Select(t => new TodoResponse(t.Id, t.Title, t.IsDone))
```

`Select` turns each row into a new shape, which is called a projection. EF Core reads the record's constructor and only asks the database for those three columns. When `Todo` grows a large `Notes` column later, this endpoint does not load it. Returning a record instead of the entity also keeps relationships out of your JSON, as explained on [Relationships](/web-api/relationships).

## Read-only queries and AsNoTracking

```csharp
var todo = await db.Todos
    .AsNoTracking()
    .FirstOrDefaultAsync(t => t.Id == id);
```

By default EF Core tracks every entity it loads, so it can spot changes when you call `SaveChangesAsync`. If you only read the data, `AsNoTracking` skips that work and uses less memory. Queries that end in `Select` into a record are not tracked anyway, so you do not need it there.

## Any and Count

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
// in MapTodoEndpoints
group.MapGet("/stats", GetStats);

// in the TodoEndpoints class
static async Task<Ok<TodoStats>> GetStats(AppDbContext db)
{
    var total = await db.Todos.CountAsync();
    var done = await db.Todos.CountAsync(t => t.IsDone);

    return TypedResults.Ok(new TodoStats(total, done, total - done));
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet("stats")]
public async Task<ActionResult<TodoStats>> GetStats()
{
    var total = await db.Todos.CountAsync();
    var done = await db.Todos.CountAsync(t => t.IsDone);

    return Ok(new TodoStats(total, done, total - done));
}
```

</div>

`CountAsync` returns the number of matching rows without loading them. `AnyAsync` answers "is there at least one?", which is faster than counting when you only need yes or no:

<div class="only-minimal">

```csharp
if (await db.Todos.AnyAsync(t => t.Title == input.Title))
{
    return TypedResults.Conflict();
}
```

Use this kind of check for rules that need the database, as the [Validation](/web-api/validation) page suggests. Add `Conflict` to the handler's `Results<...>` return type.

</div>

<div class="only-controllers">

```csharp
if (await db.Todos.AnyAsync(t => t.Title == input.Title))
{
    return Conflict();
}
```

Use this kind of check for rules that need the database, as the [Validation](/web-api/validation) page suggests. `ActionResult<Todo>` already accepts `Conflict()`, so the return type stays the same.

</div>

## Combining with pagination

If you added [Pagination](/web-api/pagination), apply the filters and the sort first, then count, then page:

```csharp
var totalCount = await todos.CountAsync();
var items = await todos
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(t => new TodoResponse(t.Id, t.Title, t.IsDone))
    .ToListAsync();
```

Counting after the filters makes `TotalCount` match what the client asked for. To accept both sets of parameters, add `Page` and `PageSize` to `TodoQuery` and return a `PagedResult<TodoResponse>`:

```csharp [Models/TodoQuery.cs (excerpt)]
public record TodoQuery(
    bool? IsDone = null,
    string? Search = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = 20);
```

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
static async Task<Ok<PagedResult<TodoResponse>>> GetAll([AsParameters] TodoQuery query, AppDbContext db)
{
    var page = Math.Max(query.Page, 1);
    var pageSize = Math.Clamp(query.PageSize, 1, 100);

    // The filters and the sort from above stay the same.

    var totalCount = await todos.CountAsync();
    // The Skip, Take, and Select from above.

    return TypedResults.Ok(new PagedResult<TodoResponse>(items, page, pageSize, totalCount));
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet]
public async Task<ActionResult<PagedResult<TodoResponse>>> GetAll([FromQuery] TodoQuery query)
{
    var page = Math.Max(query.Page, 1);
    var pageSize = Math.Clamp(query.PageSize, 1, 100);

    // The filters and the sort from above stay the same.

    var totalCount = await todos.CountAsync();
    // The Skip, Take, and Select from above.

    return new PagedResult<TodoResponse>(items, page, pageSize, totalCount);
}
```

</div>

This is the `GetAll` the rest of the guide builds on. `GET /todos` now returns a `PagedResult<TodoResponse>`. You can delete `PageQuery`, because `TodoQuery` carries the paging values.

::: tip
Turn on SQL logging to see what EF Core sends. Set `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` under `Logging:LogLevel` in `appsettings.Development.json`.
:::

## Learn more

- [Querying data with EF Core](https://learn.microsoft.com/ef/core/querying/)
- [Tracking vs. no-tracking queries](https://learn.microsoft.com/ef/core/querying/tracking)
