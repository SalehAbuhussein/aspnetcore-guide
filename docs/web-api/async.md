# Async and cancellation

## Introduction

A web server has a limited number of threads. If each request holds one while it waits for the database, the server runs out under load and requests queue up. Async code and cancellation tokens fix both halves of that: threads are freed while waiting, and work stops when nobody is waiting for the answer. You need this in every API that talks to a database or another service, which is almost all of them.

<ApiStyle />

## Async in plain words

```csharp
var todos = await db.Todos.ToListAsync();
```

`await` means "start this, give my thread back to the server, and continue here when the result is ready." While SQLite reads the file, the thread serves other requests. A method that uses `await` is marked `async` and returns a `Task` or `Task<T>`, which is an object that stands for work that finishes later.

Async does not make one request faster. It lets the same server handle many more requests at once.

## Async all the way

Once one method is async, its callers should be async too, up to the handler. ASP.NET Core awaits handlers that return `Task`, so the chain ends there.

```csharp
// Do not do this
var todos = db.Todos.ToListAsync().Result;
var count = db.Todos.CountAsync().GetAwaiter().GetResult();
```

`.Result`, `.Wait()` and `.GetAwaiter().GetResult()` block the thread until the task finishes. You get the cost of async with none of the benefit, and under load it can starve the thread pool so the whole app stalls. If you are inside a method that cannot be async, the fix is to make it async, not to block.

Two smaller rules:

1. Avoid `async void`. Exceptions from it crash the process instead of reaching your error handler. Use `async Task`.
2. Do not wrap sync code in `Task.Run` inside a handler. It moves the work to another thread from the same pool and gains nothing.

## Cancellation tokens

When a client closes the connection or times out, the server keeps running your query unless you tell it to stop. A `CancellationToken` is that signal.

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
static async Task<Ok<List<Todo>>> GetAll(AppDbContext db, CancellationToken ct)
{
    var todos = await db.Todos.AsNoTracking().ToListAsync(ct);
    return TypedResults.Ok(todos);
}
```

Add a `CancellationToken` parameter to any Minimal API handler and ASP.NET Core binds it for you. It is the same token as `HttpContext.RequestAborted`, which fires when the client goes away. Controllers work the same way with a `CancellationToken` action parameter.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet]
public async Task<ActionResult<List<Todo>>> GetAll(CancellationToken ct)
{
    var todos = await db.Todos.AsNoTracking().ToListAsync(ct);
    return Ok(todos);
}
```

Add a `CancellationToken` parameter to any action and ASP.NET Core binds it for you. It is the same token as `HttpContext.RequestAborted`, which fires when the client goes away.

</div>

Pass the token to every async call that accepts one: `ToListAsync(ct)`, `FirstOrDefaultAsync(..., ct)`, `SaveChangesAsync(ct)`, `GetFromJsonAsync(..., ct)`. EF Core then cancels the database command, and the request ends with an `OperationCanceledException` that ASP.NET Core treats as a cancelled request.

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
static async Task<Created<Todo>> Create(CreateTodo input, AppDbContext db, CancellationToken ct)
{
    var todo = new Todo { Title = input.Title };
    db.Todos.Add(todo);
    await db.SaveChangesAsync(ct);

    return TypedResults.Created($"/todos/{todo.Id}", todo);
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpPost]
public async Task<ActionResult<Todo>> Create(CreateTodo input, CancellationToken ct)
{
    var todo = new Todo { Title = input.Title };
    db.Todos.Add(todo);
    await db.SaveChangesAsync(ct);

    return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
}
```

</div>

::: tip
Some teams pass `CancellationToken.None` to `SaveChangesAsync` so a write is not abandoned halfway when the client disconnects. A single `SaveChangesAsync` runs in one transaction, so cancelling it does not leave half the rows written. Passing `ct` is fine for most writes.
:::

## Running work at the same time

When a handler makes several independent calls, start them all and wait for them together:

```csharp
var holidaysTask = holidays.GetAsync(2026, "US", ct);
var weatherTask = weather.GetForecastAsync("Dubai", ct);

await Task.WhenAll(holidaysTask, weatherTask);

var days = holidaysTask.Result;
var forecast = weatherTask.Result;
```

The total time is the slowest call, not the sum of both. Reading `.Result` after `Task.WhenAll` is safe because both tasks have already finished. `holidays` and `weather` stand for typed HTTP clients like the one on [Calling other APIs](/web-api/http-clients).

::: warning
Never run two queries at the same time on the same `DbContext`. It is not thread safe, and EF Core throws "A second operation was started on this context instance before a previous operation completed." Await database calls one after another. If you truly need parallel queries, create a separate context for each with `IDbContextFactory<AppDbContext>`.
:::

Running queries one after another on one context is usually fast enough. A slow endpoint is more often fixed by a better query than by parallel ones. See [EF Core performance](/web-api/ef-performance).

## Learn more

- [Asynchronous programming with async and await](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/)
- [ASP.NET Core best practices](https://learn.microsoft.com/aspnet/core/fundamentals/best-practices)
