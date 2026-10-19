# Validation

## Introduction

Validation checks that incoming data makes sense before your code uses it. ASP.NET Core can validate requests for you and send back a 400 response with a list of errors.

<ApiStyle />

## Turn on validation

<div class="only-minimal">

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidation();
builder.Services.AddOpenApi();

var app = builder.Build();
```

`AddValidation` tells ASP.NET Core to check the parameters of every Minimal API endpoint before the handler runs. It is new in .NET 10. Without this line, the validation attributes below have no effect on Minimal APIs.

</div>

<div class="only-controllers">

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();
```

Controllers do not need `AddValidation`, which is for Minimal APIs. When a controller has the `[ApiController]` attribute, ASP.NET Core checks the validation attributes below during model binding. If any rule fails, it returns a 400 response before your action runs.

</div>

## Add rules with data annotations

```csharp [Models/TodoDtos.cs]
using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models;

public record CreateTodo(
    [Required, StringLength(100)] string Title);

public class UpdateTodo
{
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = "";

    public bool IsDone { get; set; }
}
```

Data annotations are attributes from `System.ComponentModel.DataAnnotations` that describe a rule. `[Required]` rejects a missing value or an empty string. `[StringLength(100)]` rejects text longer than 100 characters. On a record you put them on the constructor parameters. On a class you put them on the properties.

Other common annotations:

| Attribute | Rule |
|---|---|
| `[Required]` | Value must be present |
| `[StringLength(max, MinimumLength = min)]` | Text length limits |
| `[Range(min, max)]` | Number must be in range |
| `[EmailAddress]` | Must look like an email address |
| `[RegularExpression("...")]` | Must match a pattern |

## Use the DTO in an endpoint

<div class="only-minimal">

Change `Create` and `GetById` in `Endpoints/TodoEndpoints.cs` from the [Minimal APIs](/web-api/minimal-apis) page:

```csharp [Endpoints/TodoEndpoints.cs]
using System.ComponentModel.DataAnnotations;

// inside the TodoEndpoints class
static Results<Ok<Todo>, NotFound> GetById([Range(1, int.MaxValue)] int id)
{
    var todo = Todos.Find(t => t.Id == id);
    return todo is null ? TypedResults.NotFound() : TypedResults.Ok(todo);
}

static Created<Todo> Create(CreateTodo input)
{
    var todo = new Todo { Id = Todos.Count + 1, Title = input.Title };
    Todos.Add(todo);
    return TypedResults.Created($"/todos/{todo.Id}", todo);
}
```

The routes in `MapTodoEndpoints` stay the same. If the body breaks a rule, the handler never runs.

</div>

<div class="only-controllers">

Change `Create` and `GetById` in `Controllers/TodosController.cs` from the [Controllers](/web-api/controllers) page:

```csharp [Controllers/TodosController.cs]
using System.ComponentModel.DataAnnotations;

// inside the TodosController class
[HttpGet("{id:int}")]
public ActionResult<Todo> GetById([Range(1, int.MaxValue)] int id)
{
    var todo = Todos.Find(t => t.Id == id);
    return todo is null ? NotFound() : todo;
}

[HttpPost]
public ActionResult<Todo> Create(CreateTodo input)
{
    var todo = new Todo { Id = Todos.Count + 1, Title = input.Title };
    Todos.Add(todo);
    return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
}
```

The route attributes stay the same. If the body breaks a rule, the action never runs.

</div>

Annotations also work on route and query parameters, as with `id` here. The `using System.ComponentModel.DataAnnotations;` line goes at the top of the file, for `[Range]`.

## The error response

Send a todo with an empty title:

```http [TodoApi.http]
POST {{TodoApi_HostAddress}}/todos
Content-Type: application/json

{
  "title": ""
}
```

The API answers with status 400 and a ProblemDetails body. ProblemDetails is a standard JSON shape for errors, defined in RFC 9457. It has a `title`, a `status`, and an `errors` object that maps each invalid field to a list of messages, such as "The Title field is required". Clients can read `errors` to show messages next to the right form fields.

<div class="only-minimal">

::: tip
To skip validation on one endpoint, add `.DisableValidation()` after the `Map` call.
:::

</div>

## Rules that need data

<div class="only-minimal">

Annotations check the shape of one request. A rule such as "titles must be unique" needs the database, so check it inside the handler and return `TypedResults.Conflict()` or `TypedResults.ValidationProblem(...)` yourself.

</div>

<div class="only-controllers">

Annotations check the shape of one request. A rule such as "titles must be unique" needs the database, so check it inside the action and return `Conflict()` or `ValidationProblem(...)` yourself.

</div>

::: warning
Validation only runs on endpoint parameters. If you build a `Todo` in code and save it, nothing checks it. Validate the DTO the client sends, then copy its values to your model.
:::

## Learn more

- [Minimal APIs quick reference](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
- [Model validation in ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/mvc/models/validation)
