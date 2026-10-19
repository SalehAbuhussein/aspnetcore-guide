# Glossary

## Introduction

Short definitions of the terms used across the MVC pages, in alphabetical order. Each row links to the page that explains the term in context.

| Term | Meaning | Page |
| --- | --- | --- |
| Action | A public method on a controller that handles a request and returns a result. | [Controllers](/mvc/controllers) |
| Antiforgery token | A hidden form field and cookie pair that proves a POST came from your own form. Checked by `[ValidateAntiForgeryToken]`. | [Forms](/mvc/forms) |
| appsettings.json | The JSON file that holds the app's settings. `appsettings.Development.json` overrides it in development. | [Configuration](/mvc/configuration) |
| async / await | C# keywords that let a method wait for I/O, such as a database call, without blocking a thread. | [Async](/mvc/async) |
| Authentication | Working out who the user is, from a cookie, token, or login. | [Authentication](/mvc/authentication) |
| Authorization | Deciding whether the signed-in user may do something. | [Authorization](/mvc/authorization) |
| CancellationToken | A value passed to async methods that tells them to stop when the request is aborted. | [Async](/mvc/async) |
| Claim | One fact about a user, such as their id, email, or role, stored in their identity. | [Authorization](/mvc/authorization) |
| Configuration | Settings read from appsettings files, environment variables, user secrets, and the command line, merged into one view. | [Configuration](/mvc/configuration) |
| Connection string | The text that tells EF Core which database to open and how. | [Database](/mvc/database) |
| Controller | A class whose actions handle requests and return views or redirects. | [Controllers](/mvc/controllers) |
| DbContext | The EF Core class that represents a session with the database. You query and save through it. | [Database](/mvc/database) |
| DbSet | A property on the `DbContext` that represents one table, such as `db.Books`. | [Database](/mvc/database) |
| Dependency injection (DI) | ASP.NET Core creates the services a class needs and passes them to its constructor. | [Dependency injection](/mvc/dependency-injection) |
| Endpoint | Something the app can route a request to, such as a controller action or a Minimal API handler. | [Routing](/mvc/routing) |
| Entity | A class that EF Core maps to a database table, such as `Book`. | [Database](/mvc/database) |
| Environment | The name the app runs under, usually `Development` or `Production`, set by `ASPNETCORE_ENVIRONMENT`. | [Configuration](/mvc/configuration) |
| Health check | An endpoint such as `/health` that reports whether the app and its database are working. | [Health checks](/mvc/health-checks) |
| Hot reload | Applying code changes to a running app without restarting it, through `dotnet watch`. | [CLI cheat sheet](/mvc/cli) |
| HttpContext | The object that holds everything about the current request and response. | [Lifecycle](/mvc/lifecycle) |
| IHostedService | An interface for code that starts with the app and runs in the background. `BackgroundService` implements it. | [Background tasks](/mvc/background-tasks) |
| IOptions&lt;T&gt; | A way to read a section of configuration as a typed C# class. | [Configuration](/mvc/configuration) |
| Kestrel | The web server built into ASP.NET Core that listens for HTTP requests. | [Lifecycle](/mvc/lifecycle) |
| Layout | A shared Razor file, `_Layout.cshtml`, that wraps every view with the same header and footer. | [Layouts](/mvc/layouts) |
| LINQ | Language Integrated Query: C# methods such as `Where` and `OrderBy` that EF Core turns into SQL. | [Querying](/mvc/querying) |
| Middleware | A component in the request pipeline that can inspect or change each request and response. | [Middleware](/mvc/middleware) |
| Migration | A C# file that describes one change to the database schema, created with `dotnet ef migrations add`. | [Migrations](/mvc/migrations) |
| Model binding | Filling action parameters from the route, query string, form, or body. | [Forms](/mvc/forms) |
| ModelState | The result of model binding and validation for the current request. `ModelState.IsValid` is false when a rule failed. | [Validation](/mvc/validation) |
| Navigation property | A property that points to a related entity, such as ``Review.Book``. | [Relationships](/mvc/relationships) |
| NuGet | The package manager for .NET. `dotnet add package` installs from it. | [Installation](/mvc/installation) |
| Policy | A named authorization rule, such as "must have the Admin role", applied with `[Authorize(Policy = ...)]`. | [Authorization](/mvc/authorization) |
| Program.cs | The file that builds the app: registers services, then sets up middleware and endpoints. | [Project structure](/mvc/project-structure) |
| Rate limiting | Capping how many requests a client can make in a time window. | [Rate limiting](/mvc/rate-limiting) |
| Razor | The syntax that mixes C# into HTML in `.cshtml` files. | [Views](/mvc/views) |
| Role | A group name, such as `Admin`, stored as a claim and used in authorization. | [Authorization](/mvc/authorization) |
| Route | A URL pattern, such as ``/Books/Details/{id}``, that maps to an endpoint. | [Routing](/mvc/routing) |
| Route constraint | A rule in a route, such as `{id:int}`, that limits which URLs match. | [Routing](/mvc/routing) |
| Scoped | A service lifetime: one instance per request. `DbContext` is scoped. | [Dependency injection](/mvc/dependency-injection) |
| Seeding | Filling the database with starting data. | [Seeding](/mvc/seeding) |
| Service | A class registered in the DI container so other classes can receive it. | [Dependency injection](/mvc/dependency-injection) |
| Singleton | A service lifetime: one instance for the whole app. | [Dependency injection](/mvc/dependency-injection) |
| Tag helper | A Razor feature that lets C# run inside HTML-like tags, such as `asp-for` or `asp-action`. | [Forms](/mvc/forms) |
| TempData | Data that survives one redirect, often used for "Book saved" messages. | [Sessions](/mvc/sessions) |
| Transient | A service lifetime: a new instance every time one is requested. | [Dependency injection](/mvc/dependency-injection) |
| User secrets | A per-user file outside the project for development secrets, managed with `dotnet user-secrets`. | [Configuration](/mvc/configuration) |
| Validation | Checking incoming data against rules such as `[Required]` before using it. | [Validation](/mvc/validation) |
| View | A `.cshtml` file that renders HTML for an action. | [Views](/mvc/views) |
| View component | A reusable piece of UI with its own logic, such as a sidebar list. | [View components](/mvc/view-components) |
| View model | A class shaped for one view, separate from the entity. | [Models](/mvc/models) |

## Learn more

- [Troubleshooting](/mvc/troubleshooting) for the errors you are most likely to meet.
- [ASP.NET Core fundamentals](https://learn.microsoft.com/aspnet/core/fundamentals/) on Microsoft Learn.
