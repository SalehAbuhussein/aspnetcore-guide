# Glossary

## Introduction

Short definitions of the terms used across the Web API pages, in alphabetical order. Each row links to the page that explains the term in context.

| Term | Meaning | Page |
| --- | --- | --- |
| Action | A public method on a controller that handles a request and returns a result. | [Controllers](/web-api/controllers) |
| appsettings.json | The JSON file that holds the app's settings. `appsettings.Development.json` overrides it in development. | [Configuration](/web-api/configuration) |
| async / await | C# keywords that let a method wait for I/O, such as a database call, without blocking a thread. | [Async](/web-api/async) |
| Authentication | Working out who the user is, from a cookie, token, or login. | [Authentication](/web-api/authentication) |
| Authorization | Deciding whether the signed-in user may do something. | [Authorization](/web-api/authorization) |
| CancellationToken | A value passed to async methods that tells them to stop when the request is aborted. | [Async](/web-api/async) |
| Claim | One fact about a user, such as their id, email, or role, stored in their identity. | [Authorization](/web-api/authorization) |
| Configuration | Settings read from appsettings files, environment variables, user secrets, and the command line, merged into one view. | [Configuration](/web-api/configuration) |
| Connection string | The text that tells EF Core which database to open and how. | [Database](/web-api/database) |
| Controller | A class that groups related actions. API controllers derive from `ControllerBase`. | [Controllers](/web-api/controllers) |
| CORS | Cross-Origin Resource Sharing: browser rules that decide whether a page on another origin may call your API. | [CORS](/web-api/cors) |
| DbContext | The EF Core class that represents a session with the database. You query and save through it. | [Database](/web-api/database) |
| DbSet | A property on the `DbContext` that represents one table, such as `db.Todos`. | [Database](/web-api/database) |
| Dependency injection (DI) | ASP.NET Core creates the services a class needs and passes them to its constructor. | [Dependency injection](/web-api/dependency-injection) |
| DTO | Data transfer object: a type that describes the shape of a request or response body. | [Requests](/web-api/requests) |
| Endpoint | Something the app can route a request to, such as a controller action or a Minimal API handler. | [Routing](/web-api/routing) |
| Entity | A class that EF Core maps to a database table, such as `Todo`. | [Database](/web-api/database) |
| Environment | The name the app runs under, usually `Development` or `Production`, set by `ASPNETCORE_ENVIRONMENT`. | [Configuration](/web-api/configuration) |
| Health check | An endpoint such as `/health` that reports whether the app and its database are working. | [Health checks](/web-api/health-checks) |
| Hot reload | Applying code changes to a running app without restarting it, through `dotnet watch`. | [CLI cheat sheet](/web-api/cli) |
| HttpContext | The object that holds everything about the current request and response. | [Lifecycle](/web-api/lifecycle) |
| IHostedService | An interface for code that starts with the app and runs in the background. `BackgroundService` implements it. | [Background tasks](/web-api/background-tasks) |
| IOptions&lt;T&gt; | A way to read a section of configuration as a typed C# class. | [Configuration](/web-api/configuration) |
| JWT | JSON Web Token: a signed token the client sends in the `Authorization: Bearer` header. | [Authentication](/web-api/authentication) |
| Kestrel | The web server built into ASP.NET Core that listens for HTTP requests. | [Lifecycle](/web-api/lifecycle) |
| LINQ | Language Integrated Query: C# methods such as `Where` and `OrderBy` that EF Core turns into SQL. | [Querying](/web-api/querying) |
| Middleware | A component in the request pipeline that can inspect or change each request and response. | [Middleware](/web-api/middleware) |
| Migration | A C# file that describes one change to the database schema, created with `dotnet ef migrations add`. | [Migrations](/web-api/migrations) |
| Minimal API | An endpoint defined with `app.MapGet`, `MapPost`, and so on, with a lambda or method as the handler. | [Minimal APIs](/web-api/minimal-apis) |
| Model binding | Filling action parameters from the route, query string, form, or body. | [Requests](/web-api/requests) |
| Navigation property | A property that points to a related entity, such as ``Todo.Project``. | [Relationships](/web-api/relationships) |
| NuGet | The package manager for .NET. `dotnet add package` installs from it. | [Installation](/web-api/installation) |
| OpenAPI | A JSON description of your API's endpoints, served at `/openapi/v1.json`. | [OpenAPI](/web-api/openapi) |
| Policy | A named authorization rule, such as "must have the Admin role", applied with `[Authorize(Policy = ...)]`. | [Authorization](/web-api/authorization) |
| ProblemDetails | The standard JSON shape for error responses (RFC 9457), with `type`, `title`, `status`, and `detail`. | [Errors](/web-api/errors) |
| Program.cs | The file that builds the app: registers services, then sets up middleware and endpoints. | [Project structure](/web-api/project-structure) |
| Rate limiting | Capping how many requests a client can make in a time window. | [Rate limiting](/web-api/rate-limiting) |
| Role | A group name, such as `Admin`, stored as a claim and used in authorization. | [Authorization](/web-api/authorization) |
| Route | A URL pattern, such as ``/todos/{id}``, that maps to an endpoint. | [Routing](/web-api/routing) |
| Route constraint | A rule in a route, such as `{id:int}`, that limits which URLs match. | [Routing](/web-api/routing) |
| Route group | A set of Minimal API endpoints that share a prefix and settings, created with `MapGroup`. | [Minimal APIs](/web-api/minimal-apis) |
| Scoped | A service lifetime: one instance per request. `DbContext` is scoped. | [Dependency injection](/web-api/dependency-injection) |
| Seeding | Filling the database with starting data. | [Seeding](/web-api/seeding) |
| Service | A class registered in the DI container so other classes can receive it. | [Dependency injection](/web-api/dependency-injection) |
| Singleton | A service lifetime: one instance for the whole app. | [Dependency injection](/web-api/dependency-injection) |
| Status code | The three-digit number in a response, such as `200 OK` or `404 Not Found`. | [Responses](/web-api/responses) |
| Transient | A service lifetime: a new instance every time one is requested. | [Dependency injection](/web-api/dependency-injection) |
| TypedResults | Factory methods such as `TypedResults.Ok` that return results with a known type, so OpenAPI can describe them. | [Responses](/web-api/responses) |
| User secrets | A per-user file outside the project for development secrets, managed with `dotnet user-secrets`. | [Configuration](/web-api/configuration) |
| Validation | Checking incoming data against rules such as `[Required]` before using it. | [Validation](/web-api/validation) |

## Learn more

- [Troubleshooting](/web-api/troubleshooting) for the errors you are most likely to meet.
- [ASP.NET Core fundamentals](https://learn.microsoft.com/aspnet/core/fundamentals/) on Microsoft Learn.
