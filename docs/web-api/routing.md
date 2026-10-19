# Routing

## Introduction

Routing is how ASP.NET Core decides which endpoint handles a request. It compares the HTTP method and the URL path against the route templates you wrote, then runs the first one that matches.

<ApiStyle />

## Route templates

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/todos", () => "All todos");
app.MapGet("/todos/{id}", (int id) => $"Todo {id}");
app.MapGet("/users/{userId}/todos/{todoId}", (int userId, int todoId) =>
    $"Todo {todoId} for user {userId}");
```

A route template is the URL pattern you pass to `MapGet` and the other `Map` methods.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs]
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Route("todos")]
public class TodosController : ControllerBase
{
    [HttpGet]
    public string GetAll() => "All todos";

    [HttpGet("{id}")]
    public string GetById(int id) => $"Todo {id}";

    [HttpGet("/users/{userId}/todos/{todoId}")]
    public string GetForUser(int userId, int todoId) =>
        $"Todo {todoId} for user {userId}";
}
```

A route template is the URL pattern on an action. ASP.NET Core joins the `[Route]` template on the class with the template in `[HttpGet]` or another HTTP attribute, so `GetById` answers at `/todos/{id}`. A template that starts with `/`, like the one on `GetForUser`, ignores the class route.

</div>

Plain text like `todos` must match exactly. Text in braces, like `{id}`, is a route parameter that captures that part of the URL. ASP.NET Core passes the value to the handler parameter with the same name and converts it to the parameter's type.

Matching ignores case, so `/Todos/1` and `/todos/1` reach the same endpoint.

## Route constraints

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/todos/{id:int}", (int id) => $"Todo {id}");
app.MapGet("/todos/{slug:alpha:minlength(3)}", (string slug) => $"Todo {slug}");
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet("{id:int}")]
public string GetById(int id) => $"Todo {id}";

[HttpGet("{slug:alpha:minlength(3)}")]
public string GetBySlug(string slug) => $"Todo {slug}";
```

</div>

A constraint limits what a parameter will match. Add it after a colon. With `{id:int}`, a request to `/todos/abc` does not match this route and falls through to the next one, or returns 404.

Common constraints:

| Constraint | Matches | Example |
|---|---|---|
| `int` | Whole numbers | `{id:int}` |
| `guid` | GUID values | `{id:guid}` |
| `bool` | `true` or `false` | `{done:bool}` |
| `alpha` | Letters only | `{name:alpha}` |
| `minlength(n)` | Strings of at least n characters | `{slug:minlength(3)}` |
| `range(a,b)` | Numbers between a and b | `{page:range(1,100)}` |

::: warning
Constraints help pick a route. They are not input validation. Use the checks on the [Validation](/web-api/validation) page to reject bad data with a clear message.
:::

## Optional parameters

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/reports/{year:int?}", (int? year) =>
    year is null ? "All years" : $"Report for {year}");
```

</div>

<div class="only-controllers">

```csharp [Controllers/ReportsController.cs]
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Route("reports")]
public class ReportsController : ControllerBase
{
    [HttpGet("{year:int?}")]
    public string Get(int? year) =>
        year is null ? "All years" : $"Report for {year}";
}
```

</div>

A `?` after the parameter makes it optional. The handler parameter must be nullable, as with `int?`, or have a default value. Both `/reports` and `/reports/2026` match.

## Query strings

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/todos", (bool? done, int page = 1) =>
    $"done={done}, page={page}");
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet]
public string GetAll(bool? done, int page = 1) =>
    $"done={done}, page={page}";
```

</div>

A query string is the part of the URL after `?`, such as `/todos?done=true&page=2`. Simple parameters that are not in the route template are read from the query string. A parameter with a default value, or a nullable one, is optional. The [Requests](/web-api/requests) page covers binding in more depth.

## Grouping routes

<div class="only-minimal">

```csharp [Program.cs]
var todos = app.MapGroup("/todos");

todos.MapGet("/", () => "All todos");
todos.MapGet("/{id:int}", (int id) => $"Todo {id}");
todos.MapDelete("/{id:int}", (int id) => Results.NoContent());
```

`MapGroup` sets a shared prefix for related endpoints. Groups can nest, so `app.MapGroup("/api").MapGroup("/todos")` gives you `/api/todos`.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs]
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Route("todos")]
public class TodosController : ControllerBase
{
    [HttpGet]
    public string GetAll() => "All todos";

    [HttpGet("{id:int}")]
    public string GetById(int id) => $"Todo {id}";

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) => NoContent();
}
```

The controller class is the group. `[Route("todos")]` on the class sets a shared prefix for every action in it. Write `[Route("api/todos")]` if you want `/api/todos` instead.

</div>

## Endpoint names and links

<div class="only-minimal">

```csharp [Program.cs]
using TodoApi.Models;

var todos = app.MapGroup("/todos");

todos.MapGet("/{id:int}", (int id) => new Todo { Id = id, Title = "Sample" })
    .WithName("GetTodo");

todos.MapPost("/", (Todo todo) =>
{
    todo.Id = 42;
    return TypedResults.CreatedAtRoute(todo, "GetTodo", new { id = todo.Id });
});

app.MapGet("/links/{id:int}", (int id, LinkGenerator links) =>
    links.GetPathByName("GetTodo", new { id }));
```

`WithName` gives an endpoint a unique name. Other code can then build its URL by name instead of hard coding the path. `CreatedAtRoute` uses the name to set the `Location` header on a 201 response.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs]
using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;

namespace TodoApi.Controllers;

[ApiController]
[Route("todos")]
public class TodosController : ControllerBase
{
    [HttpGet("{id:int}", Name = "GetTodo")]
    public Todo GetById(int id) => new() { Id = id, Title = "Sample" };

    [HttpPost]
    public IActionResult Create(Todo todo)
    {
        todo.Id = 42;
        return CreatedAtRoute("GetTodo", new { id = todo.Id }, todo);
    }

    [HttpGet("/links/{id:int}")]
    public string? GetLink(int id, LinkGenerator links) =>
        links.GetPathByName("GetTodo", new { id });
}
```

`Name = "GetTodo"` gives the action a unique route name. Other code can then build its URL by name instead of hard coding the path. `CreatedAtRoute` uses the name to set the `Location` header on a 201 response. Inside the same controller, `CreatedAtAction(nameof(GetById), ...)` does the same job without a name.

</div>

`LinkGenerator` is a built-in service that turns a name and route values into a path, here `/todos/7` for `id = 7`.

Names also become the operation ID in your [OpenAPI](/web-api/openapi) document.

## Learn more

- [Routing in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/routing)
- [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview)
