# Troubleshooting

## Introduction

This page lists the errors you are most likely to hit while building `BookShelf`. Each section starts with the exact text you will see, then the cause and the fix. Search this page for a few words of your error message.

Most error text below was copied from a real .NET 10 app that triggered it. Your file paths, port numbers, and cookie names will differ.

## Port already in use

```text
System.IO.IOException: Failed to bind to address http://127.0.0.1:5199: address already in use.
```

Cause: another process is listening on that port. Usually it is an earlier `dotnet run` that is still running in another terminal, or a debugger session you did not stop.

Fix: stop the other copy with `Ctrl+C` in its terminal. If you cannot find it, close it from Task Manager (Windows) or with `lsof -i :5199` and `kill` (macOS, Linux). To use a different port for one run:

```bash
dotnet run --urls http://localhost:5050
```

## HTTPS certificate not trusted

The browser shows `NET::ERR_CERT_AUTHORITY_INVALID` or "Your connection is not private", or the app fails to start with:

```text
Unable to configure HTTPS endpoint. No server certificate was specified, and the default developer certificate could not be found or is out of date.
```

Cause: the HTTPS development certificate is missing, expired, or not trusted on this machine.

Fix:

```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

Accept the prompt, then restart the browser. On Linux, `--trust` does not cover every browser; see [Installation](/mvc/installation).

## `dotnet ef` not found

```text
Could not execute because the specified command or file was not found.
Possible reasons for this include:
  * You misspelled a built-in dotnet command.
  * You intended to execute a .NET program, but dotnet-ef does not exist.
```

Cause: the EF Core command-line tool is not installed. It does not come with the SDK.

Fix:

```bash
dotnet tool install -g dotnet-ef
```

Open a new terminal so it picks up the tools folder on your `PATH`. If you installed it as a local tool, run `dotnet tool restore` first. See [Migrations](/mvc/migrations).

## "No migrations were found" or "no such table"

```text
No migrations were found.
```

```text
Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such table: Books'.
```

Cause: the database exists but has no tables. Either you never created a migration, or you created one and did not apply it. The second message can also mean the app opens a different database file than the one you migrated, because the connection string uses a relative path.

Fix:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Run both from the project folder. Then check that `ConnectionStrings:DefaultConnection` in `appsettings.json` names the same `.db` file. See [Migrations](/mvc/migrations).

## "Unable to create a 'DbContext' of type"

```text
Unable to create a 'DbContext' of type 'AppDbContext'. The exception 'Unable to resolve service for type 'Microsoft.EntityFrameworkCore.DbContextOptions`1[BookShelf.Data.AppDbContext]' while attempting to activate 'AppDbContext'.' was thrown while attempting to create an instance.
```

Cause: `dotnet ef` builds and starts your app to find the `DbContext`, and it could not create one. The most common reason is that `Program.cs` never calls `AddDbContext<AppDbContext>`. Others: the app throws during startup, for example because a connection string is missing, or you ran the command from the wrong folder.

Fix: register the context in `Program.cs`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
```

Then check that `dotnet run` starts without errors, and run `dotnet ef` from the folder that holds `BookShelf.csproj`. See [Database](/mvc/database).

## 404 on a new page

The browser shows a blank page or "This localhost page can't be found", with status `404`.

Cause: no route matches the URL. Check these in order:

1. The controller class name ends with `Controller` and is `public`. `BooksController` answers `/Books`, not `/BooksController`.
2. The action is `public` and the URL uses its name: `/Books/Details/3` calls `Details(int id)`.
3. If the action has an attribute route such as `[Route("books/{id}")]`, the conventional `/Books/Details/3` URL no longer reaches it. Use the attribute route's URL.
4. `Program.cs` still has `app.MapControllerRoute(...)` or `app.MapControllers()` after `var app = builder.Build();`.

Fix: correct whichever one is wrong. In Development, run the app and open the URL again; there is no cache to clear. See [Routing](/mvc/routing).

## 405 Method Not Allowed

```text
HTTP/1.1 405 Method Not Allowed
```

Cause: the URL matches an action, but not for this HTTP method. A form posts to an action marked `[HttpGet]`, or a link (a GET) points to an action marked `[HttpPost]`.

Fix: make the form's `method="post"` match an action with `[HttpPost]`, and give GET requests their own action with the same name. See [Forms](/mvc/forms).

## Form posts with the validation errors you did not expect

`ModelState.IsValid` is `false` and the form shows errors such as "The Title field is required." even though you typed a title.

Cause: model binding could not match form fields to properties. The input's `name` must match the property name. A hand-written `<input name="title_text">` does not bind to `Title`. A non-nullable property that is not in the form, such as a navigation property `Book Book`, also counts as required.

Fix: use `asp-for="Title"` so the tag helper writes the right `name`. Make navigation properties nullable (`Book?`), or use a view model that has only the form's fields. Print `ModelState` errors in the view with `<div asp-validation-summary="All"></div>` to see which field failed. See [Validation](/mvc/validation).

## Antiforgery 400 on POST

The browser shows `400 Bad Request`, and the log says:

```text
Antiforgery token validation failed. The required antiforgery cookie ".AspNetCore.Antiforgery.gnixJ6zrdMc" is not present.
```

Cause: the action has `[ValidateAntiForgeryToken]`, but the request did not include a token. This happens when you write `<form>` by hand without `asp-action` or `asp-controller`, post from JavaScript, or test the action with curl.

Fix: build forms with the form tag helper, which adds the hidden token for you:

```razor
<form asp-action="Create" method="post">
```

For a JavaScript `fetch`, read the token from `@Html.AntiForgeryToken()` and send it in a `RequestVerificationToken` header. See [Forms](/mvc/forms).

## 401 vs 403

| Status | Meaning | What happens in MVC |
| --- | --- | --- |
| `401 Unauthorized` | Nobody is signed in. | Cookie authentication redirects to the login page (`302` to `/Account/Login`). |
| `403 Forbidden` | Someone is signed in but does not have permission. | Cookie authentication redirects to `/Account/AccessDenied`. |

Cause of an unexpected redirect to login: `app.UseAuthentication()` is missing or comes after `app.UseAuthorization()`, so the cookie is never read.

Cause of an unexpected access denied: the user lacks the role or claim the policy needs. Roles are read when the user signs in, so sign out and back in after you change them.

Fix: keep this order in `Program.cs`: `UseRouting`, `UseAuthentication`, `UseAuthorization`, then the route mappings. See [Authorization](/mvc/authorization).

## "A second operation was started on this context"

```text
System.InvalidOperationException: A second operation was started on this context instance before a previous operation completed. This is usually caused by different threads concurrently using the same instance of DbContext.
```

Cause: two queries ran on one `DbContext` at the same time. Usually an `await` is missing, or code uses `Task.WhenAll` over several queries on the same context. A `DbContext` handles one operation at a time.

Fix: `await` each query before starting the next:

```csharp
var books = await db.Books.ToListAsync();
var reviews = await db.Reviews.ToListAsync();
```

If you truly need parallel queries, inject `IDbContextFactory<AppDbContext>` and create one context per query. See [Async](/mvc/async).

## JSON "possible object cycle"

```text
System.Text.Json.JsonException: A possible object cycle was detected. This can either be due to a cycle or if the object depth is larger than the maximum allowed depth of 64. Consider using ReferenceHandler.Preserve on JsonSerializerOptions to support cycles. Path: $.Reviews.Book.Reviews.Book...
```

Cause: an action returns `Json(book)` where `book.Reviews` each point back to `review.Book`. The serializer follows the loop forever.

Fix: return a view model or anonymous object with only the fields the client needs:

```csharp
return Json(new { book.Id, book.Title, Reviews = book.Reviews.Select(r => new { r.Rating, r.Body }) });
```

See [Relationships](/mvc/relationships).

## Hot reload does not apply a change

You save a file while `dotnet watch` runs, and nothing changes, or the terminal says the change cannot be applied and asks whether to restart.

Cause: some edits cannot be patched into a running app, for example changing a method signature, adding a field to a generic class, or editing `Program.cs` startup code. These are called rude edits. Changes under `Views/` need a browser refresh unless browser refresh is connected.

Fix: answer `y` (yes) or `a` (always) to the restart prompt, or press `Ctrl+R` in the `dotnet watch` terminal. If you started the app with plain `dotnet run`, hot reload is not on at all; use `dotnet watch`. See [CLI cheat sheet](/mvc/cli).

## Razor view not found

```text
System.InvalidOperationException: The view 'Details' was not found. The following locations were searched:
/Views/Books/Details.cshtml
/Views/Shared/Details.cshtml
```

Cause: `return View()` looks for a file named after the action, in a folder named after the controller. The file is missing, misspelled, or in the wrong folder, or its build action is not `Content`.

Fix: create `Views/Books/Details.cshtml`, or pass the name: `return View("Show", book)`. The error lists every path it tried, so compare them with your file. See [Views](/mvc/views).

## CS8618 non-nullable warnings

```text
warning CS8618: Non-nullable property 'Title' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
```

Cause: nullable reference types are on (`<Nullable>enable</Nullable>`), and a `string` or reference-type property has no starting value, so it could be `null`.

Fix: pick the one that matches what the property means:

```csharp
public string Title { get; set; } = "";       // always has a value, starts empty
public string? Subtitle { get; set; }         // may be missing
public required string Author { get; set; }   // caller must set it
public Book? Book { get; set; }               // navigation property EF Core fills
```

See [Models](/mvc/models).

## Learn more

- [CLI cheat sheet](/mvc/cli) for the commands named on this page.
- [Troubleshoot and debug ASP.NET Core projects](https://learn.microsoft.com/aspnet/core/test/troubleshoot) on Microsoft Learn.
