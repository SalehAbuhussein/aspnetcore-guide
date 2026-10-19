# Responses

## Introduction

Every response has a status code that tells the client what happened, and often a JSON body. This page shows how to send the right status code with result types and how ASP.NET Core formats your JSON.

<ApiStyle />

## Returning objects

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/todos/first", () => new Todo { Id = 1, Title = "Buy milk" });
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet("first")]
public Todo GetFirst() => new() { Id = 1, Title = "Buy milk" };
```

</div>

When a handler returns an object, ASP.NET Core sends a 200 OK with the object as JSON. That works for the happy path. To send 404 when something is missing, or 201 after creating something, you need a result type.

## Result types

<div class="only-minimal">

```csharp [Program.cs]
app.MapGet("/todos/{id:int}", (int id) =>
{
    var todo = todos.Find(t => t.Id == id);
    return todo is null ? Results.NotFound() : Results.Ok(todo);
});
```

```csharp [Program.cs]
using Microsoft.AspNetCore.Http.HttpResults;

app.MapGet("/todos/{id:int}", Results<Ok<Todo>, NotFound> (int id) =>
{
    var todo = todos.Find(t => t.Id == id);
    return todo is null ? TypedResults.NotFound() : TypedResults.Ok(todo);
});
```

Both versions send the same responses. `Results` returns a general `IResult`, so ASP.NET Core cannot tell from the code which responses are possible. `TypedResults` returns specific types like `Ok<Todo>` and `NotFound`. Prefer `TypedResults`, because the types show up in your [OpenAPI](/web-api/openapi) document and are easier to check in tests.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet("{id:int}")]
public IActionResult GetById(int id)
{
    var todo = Todos.Find(t => t.Id == id);
    return todo is null ? NotFound() : Ok(todo);
}
```

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet("{id:int}")]
public ActionResult<Todo> GetById(int id)
{
    var todo = Todos.Find(t => t.Id == id);
    return todo is null ? NotFound() : todo;
}
```

Both versions send the same responses. `IActionResult` says nothing about the body, so ASP.NET Core cannot tell from the code what a 200 returns. `ActionResult<Todo>` means "a `Todo`, or another result such as `NotFound()`". Returning `todo` directly sends a 200 with JSON. Prefer `ActionResult<T>`, because the body type shows up in your [OpenAPI](/web-api/openapi) document.

</div>

## Declaring every response

<div class="only-minimal">

`Results<Ok<Todo>, NotFound>` is a union type. It means "this handler returns either an `Ok<Todo>` or a `NotFound`, and nothing else". If you try to return something not on the list, the code does not compile.

The lambda above declares its return type before the parameter list. You need that when a lambda returns more than one result type, because C# cannot work it out alone. A named method declares it the usual way:

```csharp [Endpoints/TodoEndpoints.cs]
static Results<Ok<Todo>, NotFound> GetById(int id)
{
    var todo = Todos.Find(t => t.Id == id);
    return todo is null ? TypedResults.NotFound() : TypedResults.Ok(todo);
}
```

`Results<>` accepts between two and six types.

</div>

<div class="only-controllers">

A controller action cannot list its status codes in the return type. Add a `[ProducesResponseType]` attribute for each one instead:

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet("{id:int}")]
[ProducesResponseType<Todo>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public IActionResult GetById(int id)
{
    var todo = Todos.Find(t => t.Id == id);
    return todo is null ? NotFound() : Ok(todo);
}
```

The attributes describe the action for OpenAPI and other tools. The compiler does not check them, so keep them in step with the code.

</div>

## Common results

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs]
static Results<Created<Todo>, BadRequest<string>> Create(CreateTodo input)
{
    if (input.Title.Contains("spam"))
    {
        return TypedResults.BadRequest("Spam is not allowed.");
    }

    var todo = new Todo { Id = Todos.Count + 1, Title = input.Title };
    Todos.Add(todo);
    return TypedResults.Created($"/todos/{todo.Id}", todo);
}

static Results<NoContent, NotFound> Delete(int id)
{
    var removed = Todos.RemoveAll(t => t.Id == id);
    return removed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
}
```

`Created` sends 201 with a `Location` header that points to the new item.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpPost]
public ActionResult<Todo> Create(CreateTodo input)
{
    if (input.Title.Contains("spam"))
    {
        return BadRequest("Spam is not allowed.");
    }

    var todo = new Todo { Id = Todos.Count + 1, Title = input.Title };
    Todos.Add(todo);
    return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
}

[HttpDelete("{id:int}")]
public IActionResult Delete(int id)
{
    var removed = Todos.RemoveAll(t => t.Id == id);
    return removed == 0 ? NotFound() : NoContent();
}
```

`CreatedAtAction` sends 201 with a `Location` header that points to the new item, built from the `GetById` action's route.

</div>

`NoContent` sends 204 with an empty body, which suits deletes and updates. `BadRequest` sends 400 with whatever you pass as the body.

## Status codes

<div class="only-minimal">

| Code | Name | Method | When to use it |
|---|---|---|---|
| 200 | OK | `TypedResults.Ok(value)` | The request worked and you return data |
| 201 | Created | `TypedResults.Created(uri, value)` | A `POST` created a new item |
| 204 | No Content | `TypedResults.NoContent()` | The request worked and there is nothing to return |
| 400 | Bad Request | `TypedResults.BadRequest(value)` | The client sent invalid data |
| 401 | Unauthorized | `TypedResults.Unauthorized()` | The client is not logged in |
| 403 | Forbidden | `TypedResults.Forbid()` | The client is logged in but not allowed |
| 404 | Not Found | `TypedResults.NotFound()` | The item does not exist |
| 409 | Conflict | `TypedResults.Conflict()` | The request clashes with current data |
| 500 | Internal Server Error | `TypedResults.Problem()` | Something failed on the server |

</div>

<div class="only-controllers">

| Code | Name | Method | When to use it |
|---|---|---|---|
| 200 | OK | `Ok(value)` | The request worked and you return data |
| 201 | Created | `CreatedAtAction(action, routeValues, value)` | A `POST` created a new item |
| 204 | No Content | `NoContent()` | The request worked and there is nothing to return |
| 400 | Bad Request | `BadRequest(value)` | The client sent invalid data |
| 401 | Unauthorized | `Unauthorized()` | The client is not logged in |
| 403 | Forbidden | `Forbid()` | The client is logged in but not allowed |
| 404 | Not Found | `NotFound()` | The item does not exist |
| 409 | Conflict | `Conflict()` | The request clashes with current data |
| 500 | Internal Server Error | `Problem()` | Something failed on the server |

</div>

The [Errors](/web-api/errors) page covers how to return errors in the standard ProblemDetails format.

## JSON naming

```json
{
  "id": 1,
  "title": "Buy milk",
  "isDone": false
}
```

C# properties use PascalCase, like `IsDone`. ASP.NET Core writes them in camelCase, like `isDone`, because that is the convention in JavaScript. When reading a request body, it matches property names without caring about case.

<div class="only-minimal">

You can change the settings for all Minimal API endpoints:

```csharp [Program.cs]
using System.Text.Json.Serialization;

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
```

</div>

<div class="only-controllers">

You can change the settings for all controllers where you register them:

```csharp [Program.cs]
using System.Text.Json.Serialization;

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

</div>

This example writes enum values as names like `"High"` instead of numbers.

## Learn more

- [Create responses in Minimal API applications](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses)
- [Controller action return types](https://learn.microsoft.com/aspnet/core/web-api/action-return-types)
