# Caching

## Introduction

When the same page or query runs hundreds of times a minute and the answer rarely changes, the database does the same work over and over. Caching keeps a copy of the answer in memory for a while. You need it when a page is slow or the database is busy. You do not need it for an app with ten users, and adding it early only adds stale-data bugs.

## Two kinds of cache

ASP.NET Core gives you two tools. HybridCache stores data, such as a list of books, and your code decides when to read it. Output caching stores a whole rendered HTML response and serves it again without running your controller at all. Start with HybridCache. Reach for output caching on public pages that look the same for every visitor.

## Installing HybridCache

```bash
dotnet add package Microsoft.Extensions.Caching.Hybrid
```

```csharp [Program.cs (excerpt)]
builder.Services.AddHybridCache();
```

HybridCache keeps entries in the app's memory. It also stops a cache stampede, which is when many requests miss the cache at the same moment and all hit the database. With HybridCache only the first request runs the query and the rest wait for its result.

## Caching a query

```csharp [Services/BookService.cs]
using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookShelf.Services;

public class BookService(AppDbContext db, HybridCache cache) : IBookService
{
    private static readonly HybridCacheEntryOptions FiveMinutes = new()
    {
        Expiration = TimeSpan.FromMinutes(5)
    };

    public async Task<List<Book>> GetAllAsync()
    {
        return await cache.GetOrCreateAsync(
            "books:all",
            async ct => await db.Books
                .AsNoTracking()
                .OrderBy(b => b.Title)
                .ToListAsync(ct),
            FiveMinutes);
    }

    public async Task<Book?> FindAsync(int id) =>
        await db.Books.FindAsync(id);

    // Methods you added on earlier pages, such as AddReviewAsync, stay here.
}
```

`GetOrCreateAsync` looks up the key `"books:all"`. On a hit it returns the stored list. On a miss it runs the lambda, stores the result for five minutes, and returns it. Pick keys that read like paths, such as `books:all` or `books:42`, so you can remove them later. `AsNoTracking` is covered on [EF Core performance](/mvc/ef-performance).

## Removing stale entries

When a book changes, delete the cached list so the next request loads fresh data.

```csharp [Services/BookService.cs (excerpt)]
public async Task UpdateAsync(Book book)
{
    db.Books.Update(book);
    await db.SaveChangesAsync();
    await cache.RemoveAsync("books:all");
}
```

Add `UpdateAsync` to `IBookService` and call it from the Edit POST action. Do the same in create and delete. The expiration still acts as a safety net if you miss a spot.

## Output caching for whole pages

Output caching skips the controller, the database, and view rendering.

```csharp [Program.cs (excerpt)]
builder.Services.AddOutputCache();

var app = builder.Build();

// ...
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();
```

Place `UseOutputCache` after `UseRouting`. If your `Program.cs` does not call `UseRouting` yet, add it before the auth lines. Then mark the actions to cache.

```csharp [Controllers/BooksController.cs (excerpt)]
using Microsoft.AspNetCore.OutputCaching;

[OutputCache(Duration = 60, Tags = new[] { "books" })]
public async Task<IActionResult> Index()
{
    return View(await books.GetAllAsync());
}
```

The first visitor's page is stored for 60 seconds and everyone else gets the copy. By default output caching skips requests from signed-in users and responses that set cookies, so a user's private page is not shown to someone else.

## Evicting by tag

A tag groups cached pages so you can clear them together.

```csharp [Controllers/BooksController.cs (excerpt)]
public class BooksController(IBookService books, IOutputCacheStore outputCache) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Book book, CancellationToken ct)
    {
        if (id != book.Id) return BadRequest();
        if (!ModelState.IsValid) return View(book);

        await books.UpdateAsync(book);
        await outputCache.EvictByTagAsync("books", ct);
        return RedirectToAction(nameof(Index));
    }
}
```

`EvictByTagAsync` removes every cached page tagged `books`. Anyone who opens the list next sees the edit.

## When not to cache

Do not cache pages that differ per user, such as a dashboard or a cart. Do not cache data that must be exact right now, such as stock levels or account balances. Do not cache a query that is already fast. Measure first with the logging on [EF Core performance](/mvc/ef-performance), and cache only what shows up as slow.

::: warning
In-memory caches live inside one app process. If you run two copies of the app behind a load balancer, each has its own cache, and removing an entry on one does not remove it on the other.
:::

## Redis as a second level

When you run more than one server, add Redis as a shared cache. HybridCache uses it as a second level behind memory with no code changes in your services.

```bash
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

```csharp [Program.cs (excerpt)]
builder.Services.AddStackExchangeRedisCache(o =>
    o.Configuration = builder.Configuration.GetConnectionString("Redis"));
builder.Services.AddHybridCache();
```

HybridCache finds the registered distributed cache and writes to both. `RemoveAsync` clears the Redis copy, so other servers miss on their next read once their short-lived memory copy expires.

::: warning
Sessions also store their data in the registered distributed cache. Once you add Redis, every request that touches the session fails if Redis is not running. Start Redis locally (`docker run -p 6379:6379 redis`) or add this code only in production.
:::

## Learn more

- [HybridCache library in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/caching/hybrid)
- [Output caching middleware in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/caching/output)
