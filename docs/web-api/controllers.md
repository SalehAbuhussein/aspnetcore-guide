# Controllers

## Introduction

A controller is a class that groups related endpoints. Each public method, called an action, handles one kind of request. This page writes the todo endpoints as a controller so you can compare the two styles.

::: tip
Every other Web API page has a "Show code as" switch at the top. Pick Controllers there and the page shows all its samples as controllers.
:::

## Register controllers

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
```

`AddControllers` registers the services controllers need. `MapControllers` finds every controller class in your project and turns its actions into endpoints. A project created with `--use-controllers` already has both lines.

## Write a controller

```csharp [Controllers/TodosController.cs]
using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;

namespace TodoApi.Controllers;

[ApiController]
[Route("todos")]
public class TodosController : ControllerBase
{
    private static readonly List<Todo> Todos = [];

    [HttpGet]
    public ActionResult<List<Todo>> GetAll() => Todos;

    [HttpGet("{id:int}")]
    public ActionResult<Todo> GetById(int id)
    {
        var todo = Todos.Find(t => t.Id == id);
        if (todo is null)
        {
            return NotFound();
        }

        return todo;
    }

    [HttpPost]
    public ActionResult<Todo> Create(Todo todo)
    {
        todo.Id = Todos.Count + 1;
        Todos.Add(todo);
        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        Todos.RemoveAll(t => t.Id == id);
        return NoContent();
    }
}
```

`ControllerBase` is the base class for API controllers. It gives you helper methods such as `NotFound()`, `NoContent()`, and `CreatedAtAction()`.

The square brackets are attributes, which attach extra information to a class or method. `[HttpGet]`, `[HttpPost]`, and `[HttpDelete]` pick the HTTP method. A template like `"{id:int}"` adds to the route.

## The route attribute

`[Route("todos")]` sets the base URL for every action in the class, so `TodosController` answers at `/todos`. That matches the Minimal API version, and the same `TodoApi.http` requests work for both styles. This guide uses that form on every page.

You will also see `[Route("api/[controller]")]`, where the `[controller]` token is replaced with the class name minus the `Controller` suffix, giving `/api/todos`.

## The ApiController attribute

`[ApiController]` turns on behavior that suits APIs:

- Complex parameters such as `Todo` are read from the JSON body without extra attributes.
- If model validation fails, the request gets an automatic 400 response before your action runs. See [Validation](/web-api/validation).
- Errors use the standard ProblemDetails JSON format.

## ActionResult&lt;T&gt;

`ActionResult<Todo>` means the action returns either a `Todo` or another result such as `NotFound()`. Returning `todo` directly sends a 200 with JSON. Use `IActionResult` when there is no body, as in `Delete`.

::: warning
`ActionResult<T>` does not work with an interface type such as `IEnumerable<Todo>` when you return the value directly. Use a concrete type like `List<Todo>` or wrap the value in `Ok(...)`.
:::

## When to use controllers

Minimal APIs and controllers can live in the same project. Pick controllers when:

- You join a team or codebase that already uses them.
- You have many endpoints that share filters or attributes and want them grouped in classes.
- You need a feature that only controllers support, such as custom model binders.

For a new API, Minimal APIs are the default choice in this guide. The other pages show Minimal API code by default. Switch to Controllers at the top of any page to see the controller version.

## Learn more

- [Create web APIs with ASP.NET Core](https://learn.microsoft.com/aspnet/core/web-api/)
- [Choose between controller-based APIs and Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/apis)
