# Testing with real databases

## Introduction

Tests that run on SQLite can pass while the same code fails in production on SQL Server or PostgreSQL. Testcontainers starts a real database in Docker for your test run and throws it away afterwards. Use it when production runs on a database server and your queries go beyond basic CRUD. If BookShelf stays on SQLite in production too, the in-memory SQLite setup from [Unit tests and mocking](/mvc/unit-testing) already tests the real thing.

## Why SQLite can mislead you

Each database engine has its own rules, and EF Core translates LINQ differently for each one. A few differences that bite in practice:

- String comparison. SQLite compares text case-sensitively by default with `=`, while SQL Server is usually case-insensitive. A `Where(b => b.Title == "dune")` finds different rows.
- Types. SQLite stores almost anything in any column and cannot do some operations on `decimal` or `DateTimeOffset`. PostgreSQL rejects values that do not fit the column.
- Translation. Some LINQ queries translate on one provider and throw on another.
- Migrations. Your SQLite migrations never run against the production engine until deploy day.

A test against the same engine you ship removes this whole class of surprise.

## Switching the app to PostgreSQL

Testcontainers can only test a provider your app supports. So the first step is moving BookShelf itself to PostgreSQL. Make this change because production runs PostgreSQL. A provider that exists only in tests proves nothing.

```bash
dotnet remove package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

```csharp [Program.cs (excerpt)]
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

```json [appsettings.json (excerpt)]
"ConnectionStrings": {
  "Default": "Host=localhost;Database=bookshelf;Username=postgres;Password=postgres"
}
```

Npgsql is the PostgreSQL provider for EF Core. Migrations are written for one provider, so delete the `Migrations` folder and create a fresh one with `dotnet ef migrations add InitialCreate`. Keep the real password in [user secrets](/mvc/security-hardening).

For SQL Server, use `Microsoft.EntityFrameworkCore.SqlServer` and `UseSqlServer` instead. The rest of this page works the same with the `Testcontainers.MsSql` package.

## Installing Testcontainers

```bash
dotnet add BookShelf.Tests package Testcontainers.PostgreSql
dotnet add BookShelf.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add BookShelf.Tests package Microsoft.EntityFrameworkCore.Sqlite
```

Testcontainers is a library that starts and stops Docker containers from test code. It needs Docker running on your machine and on your CI agent. GitHub's `ubuntu-latest` runners have Docker installed.

The app no longer references SQLite, so the test project now adds the SQLite package itself. That keeps `TestDb` from [Unit tests and mocking](/mvc/unit-testing) working. `BookShelfFactory` from the [Testing](/mvc/testing) page points the app at SQLite, which the app can no longer use, so move those tests to the `PostgresFactory` below and delete `BookShelfFactory`.

## The factory fixture

```csharp [BookShelf.Tests/PostgresFactory.cs]
using BookShelf.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace BookShelf.Tests;

public class PostgresFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
```

`WebApplicationFactory<Program>` boots your app in memory, as on the [Testing](/mvc/testing) page. `IAsyncLifetime` gives xUnit an async setup and teardown. xUnit calls `InitializeAsync` once before the first test that uses the fixture and `DisposeAsync` after the last one.

`InitializeAsync` starts the container first. Docker pulls the image on the first run, which takes a while, and later runs start in a few seconds. Testcontainers picks a free port and builds the connection string for you.

Reading `Services` is what makes the factory build the app. That calls `ConfigureWebHost`, which points the `Default` connection string at the container. Then `MigrateAsync` applies your real migrations, so tests also prove that the migrations work on PostgreSQL.

`DisposeAsync` is written as an explicit interface method because `WebApplicationFactory` already has a `DisposeAsync` with a different return type. It stops the container and removes it.

::: tip
The `postgres:17-alpine` tag pins the database version. Match the major version you run in production.
:::

## Writing tests against it

```csharp [BookShelf.Tests/BooksPostgresTests.cs]
using System.Net;
using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookShelf.Tests;

public class BooksPostgresTests(PostgresFactory factory) : IClassFixture<PostgresFactory>
{
    [Fact]
    public async Task Index_ListsSavedBook()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Books.Add(new Book { Title = "Piranesi", Author = "Susanna Clarke", Year = 2020 });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.GetAsync("/Books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Piranesi", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TitleSearch_IsCaseSensitive_OnPostgres()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Books.Add(new Book { Title = "Dune", Author = "Frank Herbert", Year = 1965 });
        await db.SaveChangesAsync();

        var found = await db.Books.AnyAsync(b => b.Title == "dune");

        Assert.False(found);
    }
}
```

The first test seeds a book through a scoped `AppDbContext` and then reads the page over HTTP. The second test documents real PostgreSQL behavior. If your search must ignore case, this test tells you to use `EF.Functions.ILike` or a case-insensitive collation.

`IClassFixture` shares one container across the tests in a class. To share one container across many classes, use an xUnit collection fixture, which saves the startup time per class.

::: warning
Tests in a class share one database, so data from one test is visible in the next. Give each test its own data, such as unique titles, and do not assert on total row counts. For a clean slate per test, delete rows at the start of each test or use a library such as Respawn.
:::

## Learn more

- [Testing against your production database system](https://learn.microsoft.com/ef/core/testing/testing-with-the-database)
- [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
