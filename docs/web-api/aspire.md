# Running locally with Aspire

## Introduction

Once your app needs a real database server, a cache, or a second service, starting everything by hand gets tedious. .NET Aspire starts all of it with one command and shows logs and traces for every part in one dashboard. This page adds Aspire to `TodoApi` and runs it with a PostgreSQL database in a container.

You need the `TodoApi` project with EF Core from [Database](/web-api/database). Running PostgreSQL also needs Docker Desktop or Podman. Aspire is for local development here. It does not change how you [deploy](/web-api/deployment).

<ApiStyle />

## What Aspire gives you

- AppHost: a small C# project that describes your app's parts, such as the API project and a database, and starts them together.
- Dashboard: a web page that shows each part's console logs, structured logs, traces, and metrics. It is the same dashboard used on [Observability](/web-api/observability), but you no longer start it yourself.
- Service discovery: the AppHost hands each project the connection strings and URLs of the parts it references, so you stop copying ports into `appsettings.json`.
- ServiceDefaults: a shared project that turns on OpenTelemetry, health checks, and `HttpClient` retries for every project with one line.

## Adding the AppHost and ServiceDefaults projects

```bash
dotnet new install Aspire.ProjectTemplates
dotnet new aspire-apphost -n TodoApi.AppHost
dotnet new aspire-servicedefaults -n TodoApi.ServiceDefaults

dotnet add TodoApi.AppHost reference TodoApi
dotnet add TodoApi reference TodoApi.ServiceDefaults
dotnet add TodoApi.AppHost package Aspire.Hosting.PostgreSQL
```

Run these in the folder that contains the `TodoApi` folder, so the three projects sit side by side. The first command installs the Aspire templates, which the .NET SDK does not include. The AppHost references `TodoApi` so it can start it, and `TodoApi` references `ServiceDefaults` so it can call its setup methods. If you have a solution file, add the two new projects with `dotnet sln add`.

## Describing the app in the AppHost

```csharp [TodoApi.AppHost/AppHost.cs]
var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("db")
    .AddDatabase("todos");

builder.AddProject<Projects.TodoApi>("api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
```

`AddPostgres("db")` runs a PostgreSQL server in a container, and `AddDatabase("todos")` creates a database named `todos` on it. `Projects.TodoApi` is a class the build generates from the project reference. `WithReference(db)` passes the database's connection string to the API project as `ConnectionStrings__todos`. `WaitFor(db)` holds the API project until the database is ready to accept connections.

## Reading the connection string in TodoApi

```bash
dotnet add TodoApi package Npgsql.EntityFrameworkCore.PostgreSQL
```

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("todos")));

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapTodoEndpoints();
```

</div>

<div class="only-controllers">

```csharp [Program.cs (excerpt)]
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("todos")));

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapControllers();
```

</div>

Keep the other lines you added on earlier pages. `GetConnectionString("todos")` reads the value that `WithReference` set, because the name matches the one in `AddDatabase`. Switching from SQLite to PostgreSQL means your existing migrations were generated for the wrong database, so delete the `Migrations` folder and add a fresh one, as on [Migrations](/web-api/migrations).

`AddServiceDefaults` turns on OpenTelemetry logs, traces, and metrics, sending them to the dashboard. It also adds default health checks and service discovery. `MapDefaultEndpoints` maps `/health` and `/alive` in Development only. If you already added OpenTelemetry from [Observability](/web-api/observability), remove that code, since ServiceDefaults now does the same job.

## Running it

```bash
dotnet run --project TodoApi.AppHost
```

The console prints a dashboard URL with a login token, such as `https://localhost:17153/login?t=...`. Open it to see the `db` container and the `api` project with their state and URLs. Send a request to the `api` URL plus `/todos`. The Console logs, Structured logs, and Traces tabs show what each part is doing. Each request to `api` appears as a trace.

Without Docker, the `db` container cannot start, so `api` never starts either. In that case, keep using SQLite and run the project on its own with `dotnet run --project TodoApi`, which ignores the AppHost.

## Learn more

- [Observability](/web-api/observability)
- [Health checks](/web-api/health-checks)
- [.NET Aspire overview](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview)
