# Localization

## Introduction

Localization means returning messages in the caller's language. An API usually only localizes text a person will read, such as error titles and validation messages. This page reads the language from the `Accept-Language` header, translates messages with `.resx` files, and explains why the JSON data itself stays the same in every language.

You need the `/todos` endpoints from [Building CRUD](/web-api/crud) and the validation rules from [Validation](/web-api/validation).

<ApiStyle />

## Registering localization

```csharp [Program.cs (excerpt)]
// before builder.Build()
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// after builder.Build(), before the endpoints
string[] cultures = ["en", "fr"];
app.UseRequestLocalization(options => options
    .SetDefaultCulture("en")
    .AddSupportedCultures(cultures)
    .AddSupportedUICultures(cultures));
```

`AddLocalization` registers `IStringLocalizer<T>` and tells it to look for resource files in the `Resources` folder. `UseRequestLocalization` is middleware that picks a culture for each request. A culture is a language plus formatting rules, such as `fr` for French. It checks the `?culture=` query string, then a culture cookie, then the `Accept-Language` header that browsers and HTTP clients send. A culture you did not list falls back to `en`.

## Adding resource files

```csharp [SharedResource.cs]
namespace TodoApi;

public class SharedResource { }
```

`SharedResource` is an empty marker class. Its only job is to name a group of translations. The resource files go in `Resources`, named after the class:

```xml [Resources/SharedResource.fr.resx]
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <data name="TodoNotFound" xml:space="preserve"><value>Tâche {0} introuvable</value></data>
  <data name="TitleRequired" xml:space="preserve"><value>Le titre est obligatoire.</value></data>
</root>
```

A `.resx` file is an XML list of named strings. Visual Studio and Rider have an editor for them, so you rarely write the XML by hand. Add `Resources/SharedResource.resx` with the same names and English values. Without it, English callers see the raw key, such as `TitleRequired`. The build compiles `.fr.resx` into a satellite assembly, a small DLL that holds only French strings.

## Localizing a ProblemDetails title

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
using Microsoft.Extensions.Localization;

static async Task<Results<Ok<Todo>, ProblemHttpResult>> GetById(
    int id, AppDbContext db, IStringLocalizer<SharedResource> localizer)
{
    return await db.Todos.FindAsync(id) is { } todo
        ? TypedResults.Ok(todo)
        : TypedResults.Problem(statusCode: 404, title: localizer["TodoNotFound", id]);
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using Microsoft.Extensions.Localization;

[ApiController]
[Route("todos")]
public class TodosController(AppDbContext db, IStringLocalizer<SharedResource> localizer) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Todo>> GetById(int id)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
            return Problem(statusCode: 404, title: localizer["TodoNotFound", id]);

        return todo;
    }
}
```

</div>

`IStringLocalizer<SharedResource>` looks up a name in the resource file for the current culture. The indexer works like `string.Format`, so `{0}` becomes the id. A request with `Accept-Language: fr` for a missing todo gets:

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5", "title": "Tâche 99 introuvable", "status": 404 }
```

## Localizing validation messages

```csharp [Models/TodoDtos.cs (excerpt)]
public record CreateTodo(
    [Required(ErrorMessage = "TitleRequired"), StringLength(100)] string Title);
```

`ErrorMessage` becomes the key that is looked up in the resource file.

<div class="only-minimal">

In .NET 10, the validation that `AddValidation` adds to Minimal APIs does not use `IStringLocalizer`. It returns the `ErrorMessage` text as written, so the response above contains `"TitleRequired"` in every language. If you need translated validation messages today, use controllers for those endpoints, or check the input yourself in the handler and build the errors from `IStringLocalizer`.

</div>

<div class="only-controllers">

```csharp [Program.cs (excerpt)]
using TodoApi;

builder.Services.AddControllers()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResource)));
```

`AddDataAnnotationsLocalization` makes `[ApiController]` validation translate each `ErrorMessage` through `IStringLocalizer`. The provider points every model at `SharedResource`, so all messages live in one file. An empty title with `Accept-Language: fr` returns:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Title": ["Le titre est obligatoire."] }
}
```

The top-level `title` stays in English because ASP.NET Core sets it, not your resources.

</div>

## JSON stays culture invariant

```json
{ "price": 1234.5, "dueDate": "2026-10-19T00:00:00" }
```

A French caller gets exactly the same numbers and dates as an English one, not `1 234,50` or `19/10/2026`. `System.Text.Json` always writes numbers with a dot and dates in ISO 8601, whatever the current culture. That is what you want. JSON is read by code, and the client's code would break if the format changed with the caller's language. Format numbers and dates for display in the client, for example with `Intl.NumberFormat` in JavaScript. Only text meant for a person, such as messages and titles, belongs in your resource files.

The current culture does still affect your own C# code, such as `price.ToString("N2")`. Use `CultureInfo.InvariantCulture` when you build strings that another program will parse.

## Trying it

```http
GET https://localhost:7287/todos/99
Accept-Language: fr
```

The `title` in the response is `Tâche 99 introuvable`. Change the header to `en` or remove it to get `Todo 99 not found`.

## Learn more

- [Validation](/web-api/validation)
- [Errors](/web-api/errors)
- [Globalization and localization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/localization)
