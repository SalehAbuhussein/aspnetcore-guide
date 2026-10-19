# OpenAPI

## Introduction

OpenAPI is a standard JSON format that describes every endpoint in an API: its URL, its parameters, and its responses. Tools read this document to show interactive docs, generate client code, and test your API. ASP.NET Core can produce it from your code.

<ApiStyle />

## Generate the document

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
// Keep the other lines you already have, such as AddDbContext and UseHttpsRedirection.
using TodoApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidation();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapTodoEndpoints();

app.Run();
```

</div>

<div class="only-controllers">

```csharp [Program.cs (excerpt)]
// Keep the other lines you already have, such as AddDbContext and UseHttpsRedirection.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
```

</div>

The `webapi` template already includes these lines and the `Microsoft.AspNetCore.OpenApi` package. `AddOpenApi` registers the services that build the document. `MapOpenApi` adds an endpoint that serves it.

Run the app and open `/openapi/v1.json`:

```bash
curl http://localhost:5123/openapi/v1.json
```

You get a JSON document that lists `/todos` and its operations. The `if` block keeps the document private to development. Remove it if you want to publish the document in production.

## Add a UI with Scalar

Since .NET 9, the template no longer includes Swagger UI. The document is only JSON. To browse and call your endpoints in a web page, add Scalar, a free open source API reference UI:

```bash
dotnet add package Scalar.AspNetCore
```

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
// Keep the other lines you already have, such as AddDbContext and UseHttpsRedirection.
using Scalar.AspNetCore;
using TodoApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidation();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapTodoEndpoints();

app.Run();
```

</div>

<div class="only-controllers">

```csharp [Program.cs (excerpt)]
// Keep the other lines you already have, such as AddDbContext and UseHttpsRedirection.
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();
```

</div>

Open `/scalar` in your browser. You see each endpoint with its parameters and response types, and you can send test requests from the page.

::: tip
Scalar reads the same `/openapi/v1.json` document. Any other OpenAPI tool can use it too, such as Swagger UI or a client generator.
:::

## Describe your endpoints

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs]
var group = app.MapGroup("/todos")
    .WithTags("Todos");

group.MapGet("/{id:int}", GetById)
    .WithName("GetTodo")
    .WithSummary("Get a todo")
    .WithDescription("Returns one todo item by its ID, or 404 if it does not exist.");
```

`WithTags` groups endpoints under a heading in the UI. `WithName` sets the operation ID, a unique name for the endpoint. `WithSummary` adds a one-line title and `WithDescription` adds longer text.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[ApiController]
[Route("todos")]
[Tags("Todos")]
public class TodosController : ControllerBase
{
    [HttpGet("{id:int}", Name = "GetTodo")]
    [EndpointSummary("Get a todo")]
    [EndpointDescription("Returns one todo item by its ID, or 404 if it does not exist.")]
    public ActionResult<Todo> GetById(int id)
    {
        var todo = Todos.Find(t => t.Id == id);
        return todo is null ? NotFound() : todo;
    }
}
```

`[Tags]` on the class groups its actions under a heading in the UI. `Name` sets the operation ID, a unique name for the endpoint. `[EndpointSummary]` adds a one-line title and `[EndpointDescription]` adds longer text.

</div>

## Response types

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs]
group.MapDelete("/{id:int}", (int id) =>
    {
        var removed = Todos.RemoveAll(t => t.Id == id);
        return removed == 0 ? Results.NotFound() : Results.NoContent();
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

group.MapPost("/", Create)
    .Produces<Todo>(StatusCodes.Status201Created);
```

When a handler returns `TypedResults` with a union type such as `Results<Ok<Todo>, NotFound>`, ASP.NET Core already knows every possible response and adds them to the document. When a handler returns a plain `IResult`, it cannot tell, so you list the responses with `Produces`. `Produces<Todo>` also records the body type.

This is one more reason to prefer `TypedResults`, covered on the [Responses](/web-api/responses) page.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpDelete("{id:int}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public IActionResult Delete(int id)
{
    var removed = Todos.RemoveAll(t => t.Id == id);
    return removed == 0 ? NotFound() : NoContent();
}

[HttpPost]
[ProducesResponseType<Todo>(StatusCodes.Status201Created)]
public IActionResult Create(CreateTodo input)
{
    var todo = new Todo { Id = Todos.Count + 1, Title = input.Title };
    Todos.Add(todo);
    return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
}
```

An action that returns `ActionResult<Todo>` adds a 200 response with a `Todo` body to the document. Any other status code, such as a 404, needs a `[ProducesResponseType]` attribute. An action that returns `IActionResult` tells ASP.NET Core nothing, so list every response. `ProducesResponseType<Todo>` also records the body type.

ASP.NET Core does not check these attributes against your code, so keep them in step with what the action returns. The [Responses](/web-api/responses) page covers `ActionResult<T>`.

</div>

## Learn more

- [Overview of OpenAPI support in ASP.NET Core API apps](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview)
- [Generate OpenAPI documents](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi)
