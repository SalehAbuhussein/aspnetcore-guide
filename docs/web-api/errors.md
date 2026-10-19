# Error handling

## Introduction

When something goes wrong, your API should return a clear, consistent error instead of a blank page or a stack trace. ASP.NET Core uses the ProblemDetails format for this. This page shows how to turn it on, handle unexpected exceptions, and return your own errors.

<ApiStyle />

## What ProblemDetails is

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Todo 42 does not exist."
}
```

ProblemDetails is a standard JSON shape for HTTP errors, defined in RFC 9457. Every error has a `status`, a short `title`, and optional `detail`. Clients can read every error the same way, whatever caused it.

## Turning on ProblemDetails

```csharp [Program.cs (excerpt)]
builder.Services.AddProblemDetails();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();
```

<div class="only-minimal">

`AddProblemDetails()` registers the service that writes ProblemDetails responses. Outside Development, `UseExceptionHandler()` catches any exception your code throws and returns a `500` ProblemDetails response instead of crashing the request. `UseStatusCodePages()` adds a ProblemDetails body to empty error responses, such as a bare `404` from `TypedResults.NotFound()`.

</div>

<div class="only-controllers">

`AddProblemDetails()` registers the service that writes ProblemDetails responses. Outside Development, `UseExceptionHandler()` catches any exception your code throws and returns a `500` ProblemDetails response instead of crashing the request. `[ApiController]` already adds a ProblemDetails body to error results such as `NotFound()`. `UseStatusCodePages()` covers the rest, such as a bare `404` for a URL that matches no route.

</div>

Put `UseExceptionHandler` first in the pipeline so it catches errors from all the [middleware](/web-api/middleware) after it. The `IsDevelopment()` check turns it off while you build, so you see the developer exception page instead.

## The developer exception page

In the Development environment, ASP.NET Core adds the developer exception page for you. It shows the exception message and stack trace, which helps while you build. A stack trace is the list of method calls that led to the error. Because of the `IsDevelopment()` check above, the exception handler does not run in Development, so this page is what you see.

In every other environment, such as Production, that page is off. `UseExceptionHandler` runs instead and returns a generic `500` with no internal details.

::: warning
Never show stack traces or exception messages to clients in Production. They can reveal file paths, SQL, and other details an attacker can use.
:::

## Returning problems from endpoints

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
static async Task<Results<NoContent, ProblemHttpResult>> Update(
    int id, UpdateTodo input, AppDbContext db, EmailQueue queue, IOptions<EmailOptions> options)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return TypedResults.Problem(
            title: "Todo not found",
            detail: $"Todo {id} does not exist.",
            statusCode: StatusCodes.Status404NotFound);
    }

    var justCompleted = !todo.IsDone && input.IsDone;

    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
    todo.CompletedAt = input.IsDone ? todo.CompletedAt ?? DateTime.UtcNow : null;
    await db.SaveChangesAsync();

    if (justCompleted)
    {
        await queue.EnqueueAsync(new EmailMessage(
            options.Value.NotifyTo, "Todo completed", $"\"{todo.Title}\" is done."));
    }

    return TypedResults.NoContent();
}
```

This is the whole `Update` so far. Only the `Problem` return is new. The `CompletedAt` line and the queued email come from [Background tasks](/web-api/background-tasks), so keep them. The file needs `using Microsoft.Extensions.Options;`, `using TodoApi.Background;`, and `using TodoApi.Services;` at the top.

`TypedResults.Problem` builds a ProblemDetails response with the status and text you choose. Use it when you want to explain an error in more detail than a bare status code. For input errors, `TypedResults.ValidationProblem` adds a list of field errors. See [Validation](/web-api/validation).

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpPut("{id:int}")]
public async Task<IActionResult> Update(int id, UpdateTodo input)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return Problem(
            title: "Todo not found",
            detail: $"Todo {id} does not exist.",
            statusCode: StatusCodes.Status404NotFound);
    }

    var justCompleted = !todo.IsDone && input.IsDone;

    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
    todo.CompletedAt = input.IsDone ? todo.CompletedAt ?? DateTime.UtcNow : null;
    await db.SaveChangesAsync();

    if (justCompleted)
    {
        await queue.EnqueueAsync(new EmailMessage(
            options.Value.NotifyTo, "Todo completed", $"\"{todo.Title}\" is done."));
    }

    return NoContent();
}
```

This is the whole `Update` so far. Only the `Problem` return is new. The `CompletedAt` line and the queued email come from [Background tasks](/web-api/background-tasks), so keep them, along with `EmailQueue` and `IOptions<EmailOptions>` in the constructor.

`Problem(...)` is a `ControllerBase` helper that builds a ProblemDetails response with the status and text you choose. Use it when you want to explain an error in more detail than a bare status code. For input errors, add them with `ModelState.AddModelError` and return `ValidationProblem()` to send a list of field errors. `[ApiController]` already does this for you when data annotations fail. See [Validation](/web-api/validation).

</div>

## Writing a custom exception handler

```csharp [Errors/NotFoundExceptionHandler.cs]
using Microsoft.AspNetCore.Diagnostics;

namespace TodoApi.Errors;

public class NotFoundExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not KeyNotFoundException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "Not found",
                Detail = exception.Message
            }
        });
    }
}
```

```csharp [Program.cs (excerpt)]
using TodoApi.Errors;

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
```

An `IExceptionHandler` lets you turn specific exceptions into specific responses. `TryHandleAsync` returns `true` when it handled the exception and `false` to let the next handler or the default `500` response take over. `AddExceptionHandler<T>()` registers the class, and `UseExceptionHandler()` calls it. Your handler only runs when `UseExceptionHandler` runs. With the `IsDevelopment()` check above, that means outside Development.

You can register several handlers. They run in the order you add them, and the first one that returns `true` wins.

::: tip

<div class="only-minimal">

Exceptions are for unexpected problems. For expected cases such as a missing todo, return `TypedResults.NotFound()` or `TypedResults.Problem` directly. It is faster and easier to read.

</div>

<div class="only-controllers">

Exceptions are for unexpected problems. For expected cases such as a missing todo, return `NotFound()` or `Problem(...)` directly. It is faster and easier to read.

</div>

:::

## Learn more

- [Handle errors in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling)
