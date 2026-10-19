# Requests

## Introduction

Your handlers need data from the request: an ID from the URL, a filter from the query string, or a new todo from the body. ASP.NET Core reads these for you and passes them in as parameters. This is called parameter binding.

<ApiStyle />

::: warning
The snippets on this page show binding on its own. Try them in a scratch project, or use a route like `/try/{id:int}`. If you paste them into TodoApi next to your existing todo endpoints, two endpoints match the same URL and requests fail. The same applies to the controller actions.
:::

## Where values come from

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/todos/{id:int}", (int id, bool? includeDetails) =>
    $"id={id}, includeDetails={includeDetails}");
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet("{id:int}")]
public string GetById(int id, bool? includeDetails) =>
    $"id={id}, includeDetails={includeDetails}";
```

</div>

ASP.NET Core picks a source for each parameter by its name and type:

- A name that appears in the route template, like `id`, comes from the route.
- Other simple types, such as `int`, `string`, or `bool`, come from the query string.
- A complex type, such as a class or record, comes from the JSON body.
- A type registered as a service comes from dependency injection.

A request to `/todos/5?includeDetails=true` binds `id` to `5` and `includeDetails` to `true`.

## Request DTOs

A DTO, or data transfer object, is a small type that describes exactly what a client sends or receives. Records work well for this. A record is a class you declare in one line, with properties set in its constructor.

```csharp [Models/TodoDtos.cs]
namespace TodoApi.Models;

public record CreateTodo(string Title);

public record UpdateTodo(string Title, bool IsDone);
```

The [Validation](/web-api/validation) page replaces this file. It adds rules to both types and turns `UpdateTodo` into a class with the same two properties, so code that uses `input.Title` and `input.IsDone` keeps working.

<div class="only-minimal">

Change `Create` in `Endpoints/TodoEndpoints.cs` from the [Minimal APIs](/web-api/minimal-apis) page so it takes a `CreateTodo`:

```csharp [Endpoints/TodoEndpoints.cs]
// inside the TodoEndpoints class, mapped with group.MapPost("/", Create)
static Created<Todo> Create(CreateTodo input)
{
    var todo = new Todo { Id = Todos.Count + 1, Title = input.Title };
    Todos.Add(todo);
    return TypedResults.Created($"/todos/{todo.Id}", todo);
}
```

</div>

<div class="only-controllers">

Change `Create` in `Controllers/TodosController.cs` from the [Controllers](/web-api/controllers) page so it takes a `CreateTodo`:

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpPost]
public ActionResult<Todo> Create(CreateTodo input)
{
    var todo = new Todo { Id = Todos.Count + 1, Title = input.Title };
    Todos.Add(todo);
    return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
}
```

</div>

The client sends `{"title": "Buy milk"}` and ASP.NET Core turns it into a `CreateTodo`. Using a DTO instead of `Todo` stops clients from setting fields they should not control, such as `Id`.

Only one parameter per endpoint can come from the body. Clients normally send a body with `POST`, `PUT`, and `PATCH` requests, not with `GET`.

## Explicit sources

<div class="only-minimal">

```csharp [Program.cs]
using Microsoft.AspNetCore.Mvc;

app.MapGet("/todos/search", (
    [FromQuery(Name = "q")] string? search,
    [FromHeader(Name = "X-Client-Version")] string? clientVersion) =>
    $"search={search}, client={clientVersion}");

app.MapPut("/todos/{id:int}", ([FromRoute] int id, [FromBody] UpdateTodo input) =>
    $"Updating {id} to {input.Title}");
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet("search")]
public string Search(
    [FromQuery(Name = "q")] string? search,
    [FromHeader(Name = "X-Client-Version")] string? clientVersion) =>
    $"search={search}, client={clientVersion}";

[HttpPut("{id:int}")]
public string Update([FromRoute] int id, [FromBody] UpdateTodo input) =>
    $"Updating {id} to {input.Title}";
```

</div>

Attributes override the default rules. `[FromQuery]`, `[FromRoute]`, `[FromHeader]`, and `[FromBody]` name the source directly, and `Name` sets a key that differs from the parameter name. Headers are never bound unless you ask with `[FromHeader]`.

## Services and HttpContext

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/whoami", (HttpContext context, ILogger<Program> logger) =>
{
    logger.LogInformation("Request from {Ip}", context.Connection.RemoteIpAddress);
    return context.Request.Headers.UserAgent.ToString();
});
```

Services are injected by type, so `ILogger<Program>` arrives without any attribute. `HttpContext` holds everything about the current request and response, which helps when no other binding fits.

</div>

<div class="only-controllers">

```csharp [Controllers/WhoAmIController.cs]
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Route("whoami")]
public class WhoAmIController(ILogger<WhoAmIController> logger) : ControllerBase
{
    [HttpGet]
    public string Get()
    {
        logger.LogInformation("Request from {Ip}", HttpContext.Connection.RemoteIpAddress);
        return Request.Headers.UserAgent.ToString();
    }
}
```

A controller receives services through its constructor, so `ILogger<WhoAmIController>` arrives without any attribute. `HttpContext` and `Request` are properties on `ControllerBase`. `HttpContext` holds everything about the current request and response, which helps when no other binding fits.

</div>

The [Dependency injection](/web-api/dependency-injection) page explains services.

## Grouping parameters

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/todos", ([AsParameters] TodoQuery query) =>
    $"page={query.Page}, size={query.PageSize}, done={query.Done}");

record TodoQuery(int Page = 1, int PageSize = 10, bool? Done = null);
```

`[AsParameters]` takes the properties of a type and binds each one as if it were its own parameter. Here all three come from the query string, so `/todos?page=2&done=true` works. Use it when a handler has many parameters.

::: tip
Types declared in `Program.cs`, like `TodoQuery`, must go below all top-level statements. Move them to their own file once they grow.
:::

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet]
public string GetAll([FromQuery] TodoQuery query) =>
    $"page={query.Page}, size={query.PageSize}, done={query.Done}";

// at the bottom of the file, outside the controller class
public record TodoQuery(int Page = 1, int PageSize = 10, bool? Done = null);
```

`[FromQuery]` on a class or record parameter binds each of its properties from the query string, so `/todos?page=2&done=true` works. Use it when an action has many parameters.

</div>

## Learn more

- [Parameter binding in Minimal API apps](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/parameter-binding)
- [Model binding in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/models/model-binding)
