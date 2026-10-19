# Caching

## Introduction

Most APIs read the same data far more often than they change it. Caching keeps a copy of a result so the next request skips the database. Add it when a read endpoint is slow or hit often. Skip it while your database handles the load fine, because every cache brings the question of when the copy goes stale.

<ApiStyle />

## Pick the right tool

| Tool | What it caches | Use it for |
| --- | --- | --- |
| HybridCache | Any object you choose, inside your code | Expensive lookups used by several endpoints |
| Output caching | The whole HTTP response of an endpoint | Public `GET` endpoints that return the same JSON to everyone |
| HTTP caching headers | Nothing on the server, the client keeps the copy | Letting browsers and proxies skip requests |

Start with output caching for simple `GET` endpoints. Reach for HybridCache when you need to cache something smaller than a full response or share it across endpoints.

## HybridCache

```bash
dotnet add package Microsoft.Extensions.Caching.Hybrid
```

```csharp [Program.cs (excerpt)]
builder.Services.AddHybridCache();
```

HybridCache keeps entries in memory and, if you register a distributed cache such as Redis, in that second store too. It also protects against a stampede: when 100 requests miss the same key at once, only one runs the database query and the rest wait for its result.

Use it in `GetById`:

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
using Microsoft.Extensions.Caching.Hybrid;

static async Task<Results<Ok<Todo>, NotFound>> GetById(
    int id, AppDbContext db, HybridCache cache, CancellationToken ct)
{
    var todo = await cache.GetOrCreateAsync(
        $"todo:{id}",
        async cancel => await db.Todos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancel),
        cancellationToken: ct);

    return todo is null ? TypedResults.NotFound() : TypedResults.Ok(todo);
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using Microsoft.Extensions.Caching.Hybrid;

public class TodosController(AppDbContext db, HybridCache cache) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Todo>> GetById(int id, CancellationToken ct)
    {
        var todo = await cache.GetOrCreateAsync(
            $"todo:{id}",
            async cancel => await db.Todos
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id, cancel),
            cancellationToken: ct);

        return todo is null ? NotFound() : Ok(todo);
    }
}
```

Add `HybridCache cache` to the controller's constructor so every action can use it.

</div>

`GetOrCreateAsync` returns the cached value for the key, or runs the factory, stores the result, and returns it. The default entry lives for 5 minutes. Pass a `HybridCacheEntryOptions` with `Expiration` to change that.

When a todo changes, remove its entry so the next read gets fresh data:

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
static async Task<Results<NoContent, NotFound>> Update(
    int id, UpdateTodo input, AppDbContext db, HybridCache cache, CancellationToken ct)
{
    var todo = await db.Todos.FindAsync([id], ct);
    if (todo is null)
    {
        return TypedResults.NotFound();
    }

    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
    await db.SaveChangesAsync(ct);
    await cache.RemoveAsync($"todo:{id}", ct);

    return TypedResults.NoContent();
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpPut("{id:int}")]
public async Task<IActionResult> Update(int id, UpdateTodo input, CancellationToken ct)
{
    var todo = await db.Todos.FindAsync([id], ct);
    if (todo is null)
    {
        return NotFound();
    }

    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
    await db.SaveChangesAsync(ct);
    await cache.RemoveAsync($"todo:{id}", ct);

    return NoContent();
}
```

</div>

Do the same in `Delete`. Remove the entry after `SaveChangesAsync`, so a request in between cannot put the old value back.

## Output caching

```csharp [Program.cs (excerpt)]
builder.Services.AddOutputCache();

var app = builder.Build();

app.UseCors();
app.UseOutputCache();
```

Output caching stores the full response, status code, headers and body, and replays it for matching requests. It is built in, so no package is needed. Call `UseOutputCache` after `UseCors` if you use CORS.

Turn it on for the list endpoint and tag the entry:

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
group.MapGet("/", GetAll)
    .CacheOutput(p => p.Expire(TimeSpan.FromMinutes(1)).Tag("todos"));
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using Microsoft.AspNetCore.OutputCaching;

[HttpGet]
[OutputCache(Duration = 60, Tags = new[] { "todos" })]
public async Task<ActionResult<List<Todo>>> GetAll(CancellationToken ct)
{
    var todos = await db.Todos.AsNoTracking().ToListAsync(ct);
    return Ok(todos);
}
```

`Duration` is in seconds. If several actions share the same settings, define a named policy once and refer to it by name:

```csharp [Program.cs (excerpt)]
builder.Services.AddOutputCache(options =>
    options.AddPolicy("Todos", policy => policy.Expire(TimeSpan.FromMinutes(1)).Tag("todos")));
```

Then use `[OutputCache(PolicyName = "Todos")]` on the action instead.

</div>

A tag is a label you can use later to remove every entry that carries it. Evict the tag in each handler that writes:

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
using Microsoft.AspNetCore.OutputCaching;

static async Task<Created<Todo>> Create(
    CreateTodo input, AppDbContext db, IOutputCacheStore store, CancellationToken ct)
{
    var todo = new Todo { Title = input.Title };
    db.Todos.Add(todo);
    await db.SaveChangesAsync(ct);
    await store.EvictByTagAsync("todos", ct);

    return TypedResults.Created($"/todos/{todo.Id}", todo);
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
public class TodosController(
    AppDbContext db, HybridCache cache, IOutputCacheStore store) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Todo>> Create(CreateTodo input, CancellationToken ct)
    {
        var todo = new Todo { Title = input.Title };
        db.Todos.Add(todo);
        await db.SaveChangesAsync(ct);
        await store.EvictByTagAsync("todos", ct);

        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
    }
}
```

</div>

Add the same `EvictByTagAsync` line to `Update` and `Delete`.

::: warning
By default, output caching only caches `GET` and `HEAD` requests that return 200, and it skips requests that carry an `Authorization` header or set cookies. That is a safe default. If you change it with a custom policy, make sure one user can never receive another user's cached response.
:::

## HTTP caching headers

The cheapest request is the one the client never sends. A `Cache-Control` header tells the client how long it may reuse a response:

<div class="only-minimal">

```csharp
httpContext.Response.Headers.CacheControl = "public, max-age=60";
```

</div>

<div class="only-controllers">

```csharp
Response.Headers.CacheControl = "public, max-age=60";
```

Inside an action, `Response` is the current response. The `[ResponseCache(Duration = 60)]` attribute on an action sets the same header for you.

</div>

An ETag is a short version string for a resource. The server sends it in an `ETag` header, the client sends it back in `If-None-Match`, and if the data has not changed the server answers `304 Not Modified` with no body. For a todo, a hash of `Id`, `Title` and `IsDone` works as an ETag, or a row version column if you have one from [Transactions and concurrency](/web-api/transactions). These headers matter most for browser clients and public APIs behind a CDN.

## Redis as a second level

With one server, the in-memory cache is enough. With several servers behind a load balancer, each one has its own memory, so they miss each other's entries and removing a key on one server leaves stale copies on the others. Add Redis as a shared store with `dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis` and `builder.Services.AddStackExchangeRedisCache(o => o.Configuration = builder.Configuration.GetConnectionString("Redis"))`. HybridCache picks up the registered distributed cache automatically and uses it as its second level. Each server still keeps its own in-memory copy until it expires, so set a short `LocalCacheExpiration` in `HybridCacheEntryOptions` for data that changes often. For output caching, the `Microsoft.AspNetCore.OutputCaching.StackExchangeRedis` package adds `AddStackExchangeRedisOutputCache`.

## Learn more

- [HybridCache library in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/caching/hybrid)
- [Output caching middleware in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/caching/output)
