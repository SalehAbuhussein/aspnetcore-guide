# Async and cancellation

## Introduction

A web server has a limited pool of threads. If each request holds a thread while it waits for the database, the pool runs dry under load and new requests queue up. Async code frees the thread while it waits. This page covers the rules that keep async code correct and shows how to stop work when the user leaves. You need it as soon as your app talks to a database, a file, or another server.

## What async does in a web app

A thread is a worker that runs your code. Most of a request's time is spent waiting on I/O, which means input and output such as a database query, a file read, or an HTTP call. When you `await` an I/O call, the thread goes back to the pool and serves other requests. When the result arrives, any free thread picks up where your code left off.

```csharp [Controllers/BooksController.cs (excerpt)]
public async Task<IActionResult> Index()
{
    var all = await books.GetAllAsync();
    return View(all);
}
```

Async does not make one request faster. It lets the same server handle many more requests at once.

## The rules

1. Always `await` a task. A task you start and never await can fail with nobody seeing the error, and it may still be running after the request ends.
2. Go async all the way. If a service method calls an async method, make the service method async and return `Task`. Then the controller awaits the service.
3. Never call `.Result` or `.Wait()` on a task. They block the thread until the task finishes, which is the exact waste async exists to avoid. Under load this starves the thread pool and the app freezes.
4. Use the `Async` versions of EF Core methods: `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`.
5. Avoid `async void`. Return `Task` so callers can await it and see exceptions.

## Cancellation tokens

When a user closes the tab or clicks away, the browser drops the connection. Without cancellation your app keeps running the query and renders HTML nobody will see. A `CancellationToken` is a signal your code can check to stop early.

Add one as an action parameter. MVC binds it to the request automatically.

```csharp [Controllers/BooksController.cs (excerpt)]
public async Task<IActionResult> Index(CancellationToken ct)
{
    var all = await books.GetAllAsync(ct);
    return View(all);
}
```

`books` is the `IBookService` from [Dependency injection](/mvc/dependency-injection). The next section adds the `ct` parameter to `GetAllAsync`. Pass the token to every async call that accepts one. When the client disconnects, EF Core cancels the database command and throws `OperationCanceledException`. You do not need to catch it, since nobody is waiting for the response anyway.

## Passing the token through services

Services should take the token as their last parameter.

```csharp [Services/IBookService.cs (excerpt)]
Task<List<Book>> GetAllAsync(CancellationToken ct = default);
```

```csharp [Services/BookService.cs (excerpt)]
public Task<List<Book>> GetAllAsync(CancellationToken ct = default) =>
    db.Books.OrderBy(b => b.Title).ToListAsync(ct);
```

The `= default` keeps old callers compiling. The controller then calls `books.GetAllAsync(ct)`. If `GetAllAsync` uses HybridCache from the [Caching](/mvc/caching) page, pass `cancellationToken: ct` to `GetOrCreateAsync` instead, and keep using the token its lambda receives.

## HttpContext.RequestAborted

The action parameter is the same token as `HttpContext.RequestAborted`. Use `RequestAborted` in places where you cannot add a parameter, such as a view component or a filter.

```csharp
var books = await db.Books.ToListAsync(HttpContext.RequestAborted);
```

::: warning
Do not pass the request token to work that must finish, such as `SaveChangesAsync` after a payment. If the user closes the tab halfway, you could end up with half-saved data. For writes that must complete, leave the token out or use a queue from [Queues and retries](/mvc/queues).
:::

## Running independent calls together

When a page needs two things that do not depend on each other, start both and await them together.

```csharp [Controllers/BooksController.cs (excerpt)]
public async Task<IActionResult> Details(int id, CancellationToken ct)
{
    var bookTask = books.FindAsync(id);
    var priceTask = priceClient.GetPriceAsync(id, ct);

    await Task.WhenAll(bookTask, priceTask);

    var book = await bookTask;
    if (book is null) return NotFound();

    ViewData["Price"] = await priceTask;
    return View(book);
}
```

The page now waits for the slower call instead of both in a row. `priceClient` stands in for a typed HTTP client like the one on [Calling other APIs](/mvc/http-clients).

Do not use `Task.WhenAll` for two queries on the same `DbContext`. A context handles one operation at a time and throws "A second operation was started on this context instance" if two overlap. It is not thread safe, because it tracks entities in one shared list. Run the two queries one after the other, or combine them into one query with a projection. If you truly need parallel database calls, register a factory with `AddDbContextFactory<AppDbContext>`, inject `IDbContextFactory<AppDbContext>`, and create a separate context for each call.

## Learn more

- [Asynchronous programming with async and await](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/)
- [ASP.NET Core best practices](https://learn.microsoft.com/aspnet/core/fundamentals/best-practices)
