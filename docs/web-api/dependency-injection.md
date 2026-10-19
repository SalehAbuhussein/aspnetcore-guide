# Dependency injection

## Introduction

Dependency injection, or DI, means a class receives the objects it needs instead of creating them itself. ASP.NET Core has a DI container built in, and it is how you get a `DbContext`, a logger, or your own services into endpoints.

<ApiStyle />

## Why use DI

A dependency is any object your code needs to do its work, such as a database context or an email sender. If an endpoint creates its dependencies with `new`, it is tied to those exact classes and is hard to test. With DI you register each service once in `Program.cs`, and the container builds it and hands it to whoever asks.

## Creating a service

```csharp [Services/ITodoService.cs]
using TodoApi.Models;

namespace TodoApi.Services;

public interface ITodoService
{
    Task<List<Todo>> GetOpenAsync();
    Task<Todo> CreateAsync(string title);
}
```

```csharp [Services/TodoService.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Services;

public class TodoService(AppDbContext db) : ITodoService
{
    public Task<List<Todo>> GetOpenAsync() =>
        db.Todos.Where(t => !t.IsDone).ToListAsync();

    public async Task<Todo> CreateAsync(string title)
    {
        var todo = new Todo { Title = title };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();
        return todo;
    }
}
```

An interface describes what a service can do without saying how. `TodoService` is one implementation, and it asks for `AppDbContext` in its constructor. `AppDbContext` is the class that talks to the database. You create it on the [Database](/web-api/database) page, so treat it here as one more registered service. The container sees that parameter and supplies the registered context.

## Registering the service

```csharp [Program.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<ITodoService, TodoService>();

var app = builder.Build();
```

`AddScoped<ITodoService, TodoService>()` tells the container "when someone asks for `ITodoService`, give them a `TodoService`." All registrations go on `builder.Services` before `builder.Build()`.

## Service lifetimes

```csharp
builder.Services.AddTransient<IClock, Clock>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddSingleton<ITodoCache, TodoCache>();
```

A lifetime controls how long the container keeps an instance before making a new one.

- Transient. A new instance every time someone asks for it. Good for small, stateless helpers.
- Scoped. One instance per HTTP request. Everything in the same request shares it. `AddDbContext` uses this lifetime, so services that use the context should be scoped too.
- Singleton. One instance for the whole life of the app. Good for caches and settings. It must be safe to use from many requests at once.

`IClock`, `Clock`, `ITodoCache`, and `TodoCache` above stand in for your own types.

::: warning
Do not inject a scoped service, such as `AppDbContext`, into a singleton. The singleton would hold one context forever and share it across requests. In Development, ASP.NET Core checks for this and throws an error at startup.
:::

## Injecting into endpoints

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/todos/open", async (ITodoService todos) =>
    TypedResults.Ok(await todos.GetOpenAsync()));

app.MapPost("/todos/quick", async (string title, ITodoService todos) =>
{
    var todo = await todos.CreateAsync(title);
    return TypedResults.Created($"/todos/{todo.Id}", todo);
});
```

Add the service as a lambda parameter. ASP.NET Core sees that `ITodoService` is registered and injects it. You do not need the `[FromServices]` attribute in Minimal APIs, because registered services are detected automatically. Here `title` comes from the query string, as in `/todos/quick?title=Milk`.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using Microsoft.AspNetCore.Mvc;
using TodoApi.Data;
using TodoApi.Models;
using TodoApi.Services;

namespace TodoApi.Controllers;

[ApiController]
[Route("todos")]
public class TodosController(AppDbContext db, ITodoService todos) : ControllerBase
{
    [HttpGet("open")]
    public async Task<ActionResult<List<Todo>>> GetOpen() =>
        await todos.GetOpenAsync();

    [HttpPost("quick")]
    public async Task<ActionResult<Todo>> CreateQuick([FromQuery] string title)
    {
        var todo = await todos.CreateAsync(title);
        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
    }

    // GetAll, GetById, Create, Update, and Delete stay as they are
}
```

Add the service as a parameter of the controller's primary constructor. ASP.NET Core sees that `ITodoService` is registered and passes it in, and every action in the class can use it. `[FromQuery]` reads `title` from the query string, as in `/todos/quick?title=Milk`.

</div>

## Injecting into controllers

<div class="only-minimal">

```csharp [Controllers/TodosController.cs]
using Microsoft.AspNetCore.Mvc;
using TodoApi.Services;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController(ITodoService todos) : ControllerBase
{
    [HttpGet("open")]
    public async Task<IActionResult> GetOpen() =>
        Ok(await todos.GetOpenAsync());
}
```

[Controllers](/web-api/controllers) receive services through their constructor. The container creates a new controller for each request and fills in the parameters.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet("open/count")]
public async Task<int> CountOpen([FromServices] ITodoService todoService) =>
    (await todoService.GetOpenAsync()).Count;
```

The container creates a new controller for each request and fills in the constructor parameters, so scoped services such as `AppDbContext` are fresh for every request. If only one action needs a service, you can ask for it as an action parameter instead. `[FromServices]` marks where the value comes from. `[ApiController]` also detects registered services without it, but the attribute makes the source clear to the reader.

</div>

## Built-in services

ASP.NET Core registers many services for you. You can inject `ILogger<T>` for [logging](/web-api/logging), `IConfiguration` for settings, and `IHttpClientFactory` after calling `AddHttpClient()`. Ask for them the same way you ask for your own services.

## Learn more

- [Dependency injection in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection)
