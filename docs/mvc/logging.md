# Logging

## Introduction

Logging means writing messages about what your app is doing, so you can see problems after they happen. ASP.NET Core has logging built in, and the default setup prints messages to the console. You write logs through the `ILogger<T>` interface.

## Writing log messages

```csharp [Controllers/BooksController.cs (excerpt)]
public class BooksController(
    AppDbContext db,
    IWebHostEnvironment env,
    EmailQueue emailQueue,
    IOptions<SmtpOptions> smtp,
    ILogger<BooksController> logger) : Controller
{
    // GET: /Books/Details/5
    public async Task<IActionResult> Details(int id)
    {
        logger.LogInformation("Loading book {BookId}", id);

        var book = await db.Books
            .Include(b => b.Reviews)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (book is null)
        {
            logger.LogWarning("Book {BookId} was not found", id);
            return NotFound();
        }

        return View(book);
    }

    // other actions as before
}
```

Add `ILogger<BooksController> logger` as the last constructor parameter. `ILogger<BooksController>` is a logger that ASP.NET Core gives you through [dependency injection](/mvc/dependency-injection). The type in angle brackets becomes the category, a label that shows which class wrote the message. You do not register anything, because the template sets up logging for you.

## Log levels

Each message has a level that says how serious it is. From least to most serious:

| Level | Method | Use it for |
| --- | --- | --- |
| Trace | `LogTrace` | Very detailed steps, usually off |
| Debug | `LogDebug` | Details useful while developing |
| Information | `LogInformation` | Normal events, such as a book being created |
| Warning | `LogWarning` | Something odd that did not stop the app |
| Error | `LogError` | A failure in the current request |
| Critical | `LogCritical` | A failure that may take down the app |

You set a minimum level in configuration. Messages below it are thrown away, so you can keep `LogDebug` calls in your code without filling up production logs.

## Structured logging

```csharp
logger.LogInformation("User {UserName} created book {BookId} titled {Title}",
    User.Identity?.Name, book.Id, book.Title);
```

The text in curly braces is a placeholder in a message template. The values fill the placeholders in order, by position, not by name. Logging providers that support structured logging also store each value as a separate named field, so you can later search for every message where `BookId` is 42.

::: warning
Do not build the message with string interpolation, as in `$"Loading book {id}"`. That produces a plain string, and the `BookId` field is lost. Always pass values as separate arguments.
:::

Use PascalCase names for placeholders and keep the same name for the same thing across your app, such as `BookId` everywhere.

## Logging exceptions

```csharp
try
{
    await db.SaveChangesAsync();
}
catch (DbUpdateException ex)
{
    logger.LogError(ex, "Could not save book {BookId}", book.Id);
    throw;
}
```

Pass the exception as the first argument. The logger then records the full error message and stack trace, the list of method calls that led to the error. `DbUpdateException` lives in the `Microsoft.EntityFrameworkCore` namespace.

## Configuring levels

```json [appsettings.json (excerpt)]
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "BookShelf": "Debug"
  }
}
```

The `LogLevel` section sets the minimum level per category. `Default` applies to everything not listed. A category name matches itself and anything that starts with it, so `BookShelf` covers `BookShelf.Controllers.BooksController` and every other class in your project.

`Microsoft.AspNetCore` is set to `Warning` to hide the framework's many routine messages. To see the SQL that EF Core sends, add `"Microsoft.EntityFrameworkCore.Database.Command": "Information"`.

`appsettings.Development.json` overrides these values when the app runs in Development. See [Configuration](/mvc/configuration) for how the files combine.

## Reading the console output

```text
info: BookShelf.Controllers.BooksController[0]
      Loading book 7
warn: BookShelf.Controllers.BooksController[0]
      Book 7 was not found
```

Each entry shows a short level name, the category, an event id in square brackets, and then the message. Run the app with `dotnet run` and watch the terminal while you click around.

::: tip
Logging inside `Program.cs` works too. Use `app.Logger.LogInformation(...)` after `builder.Build()`.
:::

## Learn more

- [Logging in .NET and ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/logging/)
- [Logging in C# and .NET](https://learn.microsoft.com/dotnet/core/extensions/logging)
