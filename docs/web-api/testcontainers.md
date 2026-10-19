# Testing with real databases

## Introduction

Tests that run on SQLite can pass while the same code fails on the PostgreSQL or SQL Server database you run in production. Testcontainers starts a real database in Docker for your test run and throws it away afterwards. You need it once production uses a database server. If you ship on SQLite, the in-memory setup from [Integration tests](/web-api/testing) is enough.

## Where SQLite and production differ

| Area | SQLite | PostgreSQL / SQL Server |
| --- | --- | --- |
| String comparison | `=` is case sensitive, `LIKE` is not | Depends on collation, often the opposite |
| `decimal` | Stored as text, cannot sort or sum in SQL | Native numeric type |
| `DateTimeOffset` | Cannot be compared or ordered in SQL | Supported |
| Concurrency | One writer at a time | Many concurrent writers, real locking |
| Migrations | Many `ALTER TABLE` operations rebuild the table | Full `ALTER TABLE` support |

Each row is a bug that only shows up in production if you only test on SQLite. EF Core also translates some LINQ differently per provider, so a query can work on one and throw on another.

## Switching the app to PostgreSQL

```bash
dotnet add TodoApi package Npgsql.EntityFrameworkCore.PostgreSQL
```

```csharp [Program.cs (excerpt)]
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

Npgsql is the EF Core provider for PostgreSQL. Swap `UseSqlite` for `UseNpgsql`, point `ConnectionStrings:Default` at your server, and remove the SQLite package. Migrations contain provider-specific SQL, so delete the `Migrations` folder and run `dotnet ef migrations add Initial` again. See [Migrations](/web-api/migrations).

This page uses PostgreSQL. `Testcontainers.MsSql` and `UseSqlServer` follow the same pattern for SQL Server.

## Installing Testcontainers

```bash
dotnet add TodoApi.Tests package Testcontainers.PostgreSql
dotnet add TodoApi.Tests package Microsoft.EntityFrameworkCore.Sqlite
```

Testcontainers is a library that starts and stops Docker containers from test code. The `PostgreSql` module knows how to start PostgreSQL, wait until it accepts connections, and build a connection string for it.

The second package keeps SQLite in the test project for the tests from [Unit tests and mocking](/web-api/unit-testing), now that the app no longer references it. Delete `TodoApiFactory` from [Integration tests](/web-api/testing), because it hands the app a SQLite connection string, and use the factory below in its place.

::: warning
Docker must be running on the machine that runs the tests, including CI. GitHub Actions `ubuntu-latest` runners have Docker installed. Tests fail at startup with a "Docker is not running" error otherwise.
:::

## A factory with a real database

```csharp [TodoApi.Tests/PostgresApiFactory.cs]
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TodoApi.Data;
using Xunit;

namespace TodoApi.Tests;

public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
```

`IAsyncLifetime` gives xUnit an async setup and teardown for the fixture. `InitializeAsync` starts the container before any test runs. Docker pulls the image the first time, which takes a while, and later runs reuse it.

`ConfigureWebHost` runs when the app first starts, which happens inside `InitializeAsync` when it touches `Services`. By then the container is up, so `GetConnectionString()` returns its real address and random port. `MigrateAsync` applies your actual migrations, so the tests also prove that your migrations work on PostgreSQL. Unlike `EnsureCreated`, this catches a migration you forgot to add.

`DisposeAsync` is implemented explicitly because `WebApplicationFactory` already has a `DisposeAsync` with a different return type. It shuts the app down first, then removes the container.

## Writing the tests

```csharp [TodoApi.Tests/TodoPostgresTests.cs]
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TodoApi.Data;
using TodoApi.Models;
using Xunit;

namespace TodoApi.Tests;

public class TodoPostgresTests(PostgresApiFactory factory)
    : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task SavedTodo_ShowsUpInList()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Todos.Add(new Todo { Title = "Real database" });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();

        var response = await client.GetAsync("/todos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var todos = await response.Content.ReadFromJsonAsync<List<Todo>>();
        Assert.Contains(todos!, t => t.Title == "Real database");
    }
}
```

The test saves a row through `AppDbContext`, then reads it back through the API. It uses `GET /todos` because that endpoint allows anonymous calls, while the other todo endpoints need a token since the [Authorization](/web-api/authorization) page. Apart from that it looks like the tests on [Integration tests](/web-api/testing). Only the fixture changed. `IClassFixture` starts one container for all tests in the class.

## Keeping it fast

Starting a container takes a few seconds, so share it. Put several test classes in one xUnit collection with `[Collection("Postgres")]` and an `ICollectionFixture<PostgresApiFactory>` definition, and they all use one container. Clean up between tests by deleting rows, or give each test unique data so it does not care what others left behind.

Keep the fast SQLite and unit tests from [Unit tests and mocking](/web-api/unit-testing) for business rules, and use the container tests for queries, migrations, and anything that depends on database behavior.

::: tip
Run the same PostgreSQL image locally during development, for example with `docker run -p 5432:5432 -e POSTGRES_PASSWORD=dev postgres:17-alpine`. Matching development, tests, and production removes a whole class of "works on my machine" bugs.
:::

## Learn more

- [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
- [Choosing a testing strategy](https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy)
