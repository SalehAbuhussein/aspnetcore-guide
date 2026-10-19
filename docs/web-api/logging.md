# Logging

## Introduction

Logging records what your app does while it runs, so you can find out what went wrong after the fact. ASP.NET Core has logging built in. By default, logs go to the console and to the debug output in your editor.

<ApiStyle />

## Writing log messages

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs]
static async Task<Results<Ok<Todo>, NotFound>> GetById(int id, AppDbContext db, ILogger<Todo> logger)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        logger.LogWarning("Todo {TodoId} was not found", id);
        return TypedResults.NotFound();
    }

    logger.LogInformation("Returning todo {TodoId}", id);
    return TypedResults.Ok(todo);
}
```

`ILogger<T>` is the logging service. Ask for it like any other [dependency](/web-api/dependency-injection), as an endpoint parameter or a constructor parameter. The `T` sets the log category, a name that tells you where a message came from. Inside a class, use the class itself, as in `ILogger<TodoService>`. A static class cannot be a type argument, so the example above uses `Todo`.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
public class TodosController(AppDbContext db, ILogger<TodosController> logger) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Todo>> GetById(int id)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            logger.LogWarning("Todo {TodoId} was not found", id);
            return NotFound();
        }

        logger.LogInformation("Returning todo {TodoId}", id);
        return todo;
    }
}
```

`ILogger<T>` is the logging service. Ask for it like any other [dependency](/web-api/dependency-injection), in the controller's constructor. The `T` sets the log category, a name that tells you where a message came from. Use the class itself, as in `ILogger<TodosController>` or `ILogger<TodoService>`.

</div>

## Log levels

| Level | Method | Use it for |
| --- | --- | --- |
| Trace | `LogTrace` | Very detailed steps, usually off |
| Debug | `LogDebug` | Details useful while developing |
| Information | `LogInformation` | Normal events, like a todo being created |
| Warning | `LogWarning` | Something odd that did not stop the request |
| Error | `LogError` | A failure in the current operation |
| Critical | `LogCritical` | The whole app is in trouble |

Each message has a level that says how serious it is. You choose a minimum level in configuration, and anything below it is skipped.

## Message templates

```csharp
logger.LogInformation("User {UserName} created todo {TodoId}", userName, todo.Id);
```

The text in braces is a placeholder. A message template is a log message with named placeholders, and the values are filled in by position. Logging providers that support structured logging store `UserName` and `TodoId` as separate fields, so you can search for every log about todo 42.

Do not build the message with string interpolation such as `$"Todo {id}"`. That bakes the value into the text and loses the named field.

```csharp
try
{
    await db.SaveChangesAsync();
}
catch (DbUpdateException ex)
{
    logger.LogError(ex, "Saving todo {TodoId} failed", todo.Id);
    throw;
}
```

Pass the exception as the first argument to `LogError`. The logger records the exception type, message, and stack trace along with your text.

## Configuring levels

```json [appsettings.json]
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

The `Logging:LogLevel` section sets the minimum level per category. `Default` applies to everything without a more specific entry. Category names match by prefix, so `Microsoft.AspNetCore` covers every category that starts with it. The EF Core line above logs every SQL command, which helps when you want to see the queries EF Core sends.

Put noisier settings in `appsettings.Development.json` so they apply only on your machine. See [Configuration](/web-api/configuration).

::: warning
Never log passwords, tokens, or other secrets. Logs are often stored for a long time and read by many people.
:::

## HTTP logging

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.HttpLogging;

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});

var app = builder.Build();

app.UseHttpLogging();
```

HTTP logging writes one entry per request with the fields you pick. `AddHttpLogging` sets the fields, and `UseHttpLogging` adds the [middleware](/web-api/middleware). These entries use the `Microsoft.AspNetCore.HttpLogging` category, which the default `Warning` level for `Microsoft.AspNetCore` hides. Add this line under `LogLevel` to see them.

```json [appsettings.Development.json]
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware": "Information"
    }
  }
}
```

HTTP logging can slow your app and can capture private data if you log headers or bodies, so keep the field list short.

## Learn more

- [Logging in .NET and ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/logging/)
- [HTTP logging in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/http-logging/)
