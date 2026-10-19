# Troubleshooting

## Introduction

This page lists the errors you are most likely to hit while building `TodoApi`. Each section starts with the exact text you will see, then the cause and the fix. Search this page for a few words of your error message.

Most error text below was copied from a real .NET 10 app that triggered it. Your file paths, port numbers, and cookie names will differ.

<ApiStyle />

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

Accept the prompt, then restart the browser. On Linux, `--trust` does not cover every browser; see [Installation](/web-api/installation).

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

Open a new terminal so it picks up the tools folder on your `PATH`. If you installed it as a local tool, run `dotnet tool restore` first. See [Migrations](/web-api/migrations).

## "No migrations were found" or "no such table"

```text
No migrations were found.
```

```text
Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such table: Todos'.
```

Cause: the database exists but has no tables. Either you never created a migration, or you created one and did not apply it. The second message can also mean the app opens a different database file than the one you migrated, because the connection string uses a relative path.

Fix:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Run both from the project folder. Then check that `ConnectionStrings:DefaultConnection` in `appsettings.json` names the same `.db` file. See [Migrations](/web-api/migrations).

## "Unable to create a 'DbContext' of type"

```text
Unable to create a 'DbContext' of type 'AppDbContext'. The exception 'Unable to resolve service for type 'Microsoft.EntityFrameworkCore.DbContextOptions`1[TodoApi.Data.AppDbContext]' while attempting to activate 'AppDbContext'.' was thrown while attempting to create an instance.
```

Cause: `dotnet ef` builds and starts your app to find the `DbContext`, and it could not create one. The most common reason is that `Program.cs` never calls `AddDbContext<AppDbContext>`. Others: the app throws during startup, for example because a connection string is missing, or you ran the command from the wrong folder.

Fix: register the context in `Program.cs`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
```

Then check that `dotnet run` starts without errors, and run `dotnet ef` from the folder that holds `TodoApi.csproj`. See [Database](/web-api/database).

## 404 on a new endpoint

```text
HTTP/1.1 404 Not Found
```

The response has no body. Cause: no route matches the URL and method you sent. Check these:

<div class="only-minimal">

1. The `Map...` call runs. If your endpoints live in `Endpoints/TodoEndpoints.cs`, `Program.cs` must call `app.MapTodoEndpoints();` before `app.Run();`.
2. A `MapGroup("/todos")` prefix is added to every route inside it, so `group.MapGet("/todos/{id}", ...)` answers `/todos/todos/5`. Inside a group, write `"/{id:int}"`.
3. A route constraint such as `{id:int}` does not match `/todos/abc`.

</div>

<div class="only-controllers">

1. `Program.cs` calls `builder.Services.AddControllers();` and `app.MapControllers();`. Without `MapControllers`, no controller route exists.
2. The class has `[ApiController]` and `[Route("todos")]`, and the action's attribute adds to it: `[HttpGet("{id:int}")]` answers `/todos/5`. Writing `[HttpGet("todos/{id:int}")]` makes it `/todos/todos/5`.
3. The class is `public` and derives from `ControllerBase`.

</div>

Fix: correct the route, then compare with the list of routes in `/openapi/v1.json`. See [Routing](/web-api/routing).

## 405 Method Not Allowed

```text
HTTP/1.1 405 Method Not Allowed
```

Cause: the URL matches an endpoint, but not for the HTTP method you sent. For example, you sent `PUT /todos` while only `PUT /todos/{id}` exists, or you opened a POST-only URL in the browser address bar, which always sends GET.

Fix: check the method and URL in your `.http` file against the endpoint. Use the `.http` file or curl for anything other than GET. See [CRUD](/web-api/crud).

## 400 with validation errors you did not expect

```json
{
  "title": "One or more validation errors occurred.",
  "errors": { "Title": ["The Title field is required."] }
}
```

Cause: either the body really broke a rule, or the JSON did not bind to your DTO. Common reasons for the second:

1. The request has no `Content-Type: application/json` header, so the body is not read as JSON.
2. A property name is misspelled in the JSON. Names are matched case-insensitively, but `titel` does not match `Title`.
3. A non-nullable property is missing from the JSON. A `string` property without `?` counts as required.

<div class="only-minimal">

If you expected validation and got none, check that `builder.Services.AddValidation();` is in `Program.cs`. Minimal APIs only check data annotations when it is.

</div>

<div class="only-controllers">

`[ApiController]` returns this 400 before your action runs, so a breakpoint in the action is never hit. Read the `errors` object to see which field failed.

</div>

Fix: send the header, correct the property names, and make optional properties nullable (`string?`). See [Validation](/web-api/validation).

## 401 vs 403

| Status | Meaning | Usual cause |
| --- | --- | --- |
| `401 Unauthorized` | The request did not prove who the caller is. | No `Authorization: Bearer ...` header, an expired token, or a token signed with a different key. |
| `403 Forbidden` | The caller is known but lacks permission. | The token has no matching role or claim for the policy. |

A `401` from JWT bearer authentication has an empty body and a `WWW-Authenticate: Bearer` header. When the token was rejected, that header includes `error="invalid_token"` and often an `error_description` that says why.

Fix for an unexpected `401`: check that `app.UseAuthentication()` comes before `app.UseAuthorization()`, that the token is sent as `Authorization: Bearer <token>`, and that the issuer, audience, and signing key in configuration match the ones that made the token. Fix for an unexpected `403`: decode the token and check its role claims against the policy. See [Authentication](/web-api/authentication) and [Authorization](/web-api/authorization).

## CORS error in the browser console

```text
Access to fetch at 'https://localhost:7001/todos' from origin 'http://localhost:4200' has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header is present on the requested resource.
```

Cause: the page that made the call runs on a different origin (scheme, host, and port) than the API, and the API did not say that origin is allowed. The same request works in curl or a `.http` file, because only browsers enforce CORS.

Fix: register a policy with the exact origin, no trailing slash, and turn it on:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
```

`app.UseCors()` must come before `UseAuthentication` and `UseAuthorization`. If the error appears only on some requests, the real problem is often a `500` from the API; check the API's console. See [CORS](/web-api/cors).

## Antiforgery error on a file upload

<div class="only-minimal">

```text
Microsoft.AspNetCore.Http.BadHttpRequestException: Invalid anti-forgery token found when reading parameter "IFormFile file" from the request body as form.
```

Or, when the antiforgery middleware is missing:

```text
System.InvalidOperationException: Endpoint HTTP: POST /upload contains anti-forgery metadata, but a middleware was not found that supports anti-forgery.
```

Cause: Minimal API endpoints that read form data, including `IFormFile`, check an antiforgery token by default. An API called from JavaScript or a mobile app with a bearer token has no token to send.

Fix: turn the check off for that endpoint:

```csharp
app.MapPost("/upload", (IFormFile file) => file.FileName)
    .DisableAntiforgery();
```

Keep it on if the API uses cookie authentication and is called from browser forms. See [File uploads](/web-api/file-uploads).

</div>

<div class="only-controllers">

Controller actions do not check antiforgery tokens unless you add `[ValidateAntiForgeryToken]`, so uploads with a bearer token work without changes. If you added that attribute, a request without a token gets a `400` and this log line:

```text
The required antiforgery cookie ".AspNetCore.Antiforgery.gnixJ6zrdMc" is not present.
```

Fix: remove the attribute for bearer-token clients, or send the token from your page. See [File uploads](/web-api/file-uploads).

</div>

## "A second operation was started on this context"

```text
System.InvalidOperationException: A second operation was started on this context instance before a previous operation completed. This is usually caused by different threads concurrently using the same instance of DbContext.
```

Cause: two queries ran on one `DbContext` at the same time. Usually an `await` is missing, or code uses `Task.WhenAll` over several queries on the same context. A `DbContext` handles one operation at a time.

Fix: `await` each query before starting the next:

```csharp
var todos = await db.Todos.ToListAsync();
var projects = await db.Projects.ToListAsync();
```

If you truly need parallel queries, inject `IDbContextFactory<AppDbContext>` and create one context per query. See [Async](/web-api/async).

## JSON "possible object cycle"

```text
System.Text.Json.JsonException: A possible object cycle was detected. This can either be due to a cycle or if the object depth is larger than the maximum allowed depth of 64. Consider using ReferenceHandler.Preserve on JsonSerializerOptions to support cycles. Path: $.Todos.Project.Todos.Project...
```

Cause: the endpoint returns an entity with navigation properties that point back at each other, such as `project.Todos[0].Project`. The serializer follows the loop until it gives up, and the client gets a `500`.

Fix: return a DTO instead of the entity, with only the fields the client needs:

```csharp
var projects = await db.Projects
    .Select(p => new { p.Id, p.Name, Todos = p.Todos.Select(t => new { t.Id, t.Title }) })
    .ToListAsync();
```

`ReferenceHandler.IgnoreCycles` also stops the error, but it hides the real problem: you are sending more data than you meant to. See [Relationships](/web-api/relationships).

## Hot reload does not apply a change

You save a file while `dotnet watch` runs, and nothing changes, or the terminal says the change cannot be applied and asks whether to restart.

Cause: some edits cannot be patched into a running app, for example changing a method signature, adding a field to a generic class, or editing `Program.cs` startup code. These are called rude edits.

Fix: answer `y` (yes) or `a` (always) to the restart prompt, or press `Ctrl+R` in the `dotnet watch` terminal. If you started the app with plain `dotnet run`, hot reload is not on at all; use `dotnet watch`. See [CLI cheat sheet](/web-api/cli).

## CS8618 non-nullable warnings

```text
warning CS8618: Non-nullable property 'Title' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
```

Cause: nullable reference types are on (`<Nullable>enable</Nullable>`), and a `string` or reference-type property has no starting value, so it could be `null`.

Fix: pick the one that matches what the property means:

```csharp
public string Title { get; set; } = "";       // always has a value, starts empty
public string? Notes { get; set; }            // may be missing
public required string Owner { get; set; }    // caller must set it
public Project? Project { get; set; }         // navigation property EF Core fills
```

See [Database](/web-api/database).

## Learn more

- [CLI cheat sheet](/web-api/cli) for the commands named on this page.
- [Troubleshoot and debug ASP.NET Core projects](https://learn.microsoft.com/aspnet/core/test/troubleshoot) on Microsoft Learn.
