# Minimal APIs

## Introduction

A Minimal API endpoint is one method call that pairs an HTTP method and a URL with a function. This page builds the first `/todos` endpoints and shows how to keep them organized as the API grows.

## Your first endpoints

```csharp [Program.cs]
using TodoApi.Models;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var todos = new List<Todo>
{
    new() { Id = 1, Title = "Buy milk" },
    new() { Id = 2, Title = "Walk the dog", IsDone = true }
};

app.MapGet("/todos", () => todos);

app.MapGet("/todos/{id}", (int id) => todos.Find(t => t.Id == id));

app.MapPost("/todos", (Todo todo) =>
{
    todo.Id = todos.Count + 1;
    todos.Add(todo);
    return todo;
});

app.MapPut("/todos/{id}", (int id, Todo input) =>
{
    var todo = todos.Find(t => t.Id == id);
    if (todo is null) return;
    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
});

app.MapDelete("/todos/{id}", (int id) => todos.RemoveAll(t => t.Id == id));

app.Run();
```

Each `Map` method takes a route and a lambda, which is a short inline function. `MapGet` handles `GET` requests, `MapPost` handles `POST`, and so on. Whatever the lambda returns, ASP.NET Core writes back as JSON.

`{id}` in the route is a route parameter. Its value is passed to the lambda parameter with the same name. A `Todo` parameter on a `POST` is read from the JSON request body. The [Requests](/web-api/requests) page covers both.

The data lives in a `List<Todo>` in memory, so it resets every time the app restarts. The [Database](/web-api/database) page replaces it with a real database.

## Route groups

Every route above starts with `/todos`. A route group lets you write that prefix once:

```csharp [Program.cs]
var group = app.MapGroup("/todos");

group.MapGet("/", () => todos);
group.MapGet("/{id}", (int id) => todos.Find(t => t.Id == id));
```

`MapGroup` returns a builder whose endpoints all share the prefix. You can also attach settings to the group, such as authorization, and every endpoint in it inherits them.

## Move endpoints out of Program.cs

`Program.cs` grows fast. Move the endpoints into their own file with an extension method, which is a static method that looks like it belongs to another type:

```csharp [Endpoints/TodoEndpoints.cs]
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Models;

namespace TodoApi.Endpoints;

public static class TodoEndpoints
{
    private static readonly List<Todo> Todos = [];

    public static void MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/todos");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create);
    }

    static Ok<List<Todo>> GetAll() => TypedResults.Ok(Todos);

    static Results<Ok<Todo>, NotFound> GetById(int id)
    {
        var todo = Todos.Find(t => t.Id == id);
        return todo is null ? TypedResults.NotFound() : TypedResults.Ok(todo);
    }

    static Created<Todo> Create(Todo todo)
    {
        todo.Id = Todos.Count + 1;
        Todos.Add(todo);
        return TypedResults.Created($"/todos/{todo.Id}", todo);
    }
}
```

```csharp [Program.cs]
using TodoApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapTodoEndpoints();

app.Run();
```

Named methods replace the lambdas, which makes each handler easier to read and test. `Program.cs` now has one line for all todo endpoints.

::: warning
A static `List<Todo>` is not safe when two requests change it at the same moment. It is fine for learning. Real apps use a database.
:::

## TypedResults

`TypedResults` builds responses with a specific status code, such as `TypedResults.Ok(todo)` for 200 or `TypedResults.NotFound()` for 404. The return type, such as `Results<Ok<Todo>, NotFound>`, lists every response the handler can send. ASP.NET Core uses that list to describe the endpoint in your [OpenAPI](/web-api/openapi) document. The [Responses](/web-api/responses) page goes through them in detail.

## Learn more

- [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview)
- [Route handlers in Minimal API apps](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/route-handlers)
