# Integration tests

## Introduction

An integration test starts your real app in memory and sends HTTP requests to it, so you test routing, middleware, and the database together. ASP.NET Core makes this possible with `WebApplicationFactory`. This page adds an xUnit test project for `TodoApi` and writes a test for `GET /todos`.

## Creating the test project

```bash
dotnet new xunit -n TodoApi.Tests
dotnet add TodoApi.Tests reference TodoApi
dotnet add TodoApi.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet new sln -n TodoApi
dotnet sln add TodoApi TodoApi.Tests
```

Run these from the folder that contains your `TodoApi` folder. xUnit is a popular .NET test framework, and `dotnet new xunit` creates a project ready to run tests. The second command adds a project reference, so the tests can see your API's code. The `Mvc.Testing` package provides `WebApplicationFactory`.

The last two commands create a solution file and add both projects to it. A solution groups projects so one command can build and test them all. In .NET 10, `dotnet new sln` creates a `TodoApi.slnx` file.

Your folders now look like this.

```text
TodoApi/
TodoApi.Tests/
TodoApi.slnx
```

## Making Program visible to tests

A top-level `Program.cs` produces a class named `Program`, and the tests need to refer to it. In .NET 10 the SDK makes `Program` public for you, so tests can use it directly.

## Writing a first test

```csharp [TodoApi.Tests/TodoEndpointsTests.cs]
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TodoApi.Models;
using Xunit;

namespace TodoApi.Tests;

public class TodoEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetTodos_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var todos = await response.Content.ReadFromJsonAsync<PagedResult<TodoResponse>>();
        Assert.NotNull(todos);
    }
}
```

`WebApplicationFactory<Program>` boots your app using its real `Program.cs`. `IClassFixture` tells xUnit to create one factory and share it across all tests in the class, which keeps tests fast. `CreateClient()` returns an `HttpClient` wired straight to the app in memory, with no network port.

`GET /todos` returns a `PagedResult<TodoResponse>` since the [Pagination](/web-api/pagination) and [Querying](/web-api/querying) pages, so the test reads that type.

Each test is a method marked with `[Fact]`. It follows the arrange, act, assert pattern. Set things up, do the action, then check the result.

```bash
dotnet test
```

Run it from the folder that holds `TodoApi.slnx`. It builds both projects and runs every test.

The test looks for `todos.db` in the test project's output folder, not in `TodoApi`. If your app migrates the database at startup, as on the [Seeding](/web-api/seeding) page, the test passes. Otherwise it fails with `no such table: Todos`. The next section fixes that by giving the tests their own database.

## Using an in-memory database

```csharp [TodoApi.Tests/TodoApiFactory.cs]
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TodoApi.Data;

namespace TodoApi.Tests;

public class TodoApiFactory : WebApplicationFactory<Program>
{
    private const string ConnectionString = "Data Source=TodoTests;Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive = new(ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _keepAlive.Dispose();
    }
}
```

Tests should not touch your real `todos.db`. This factory points the `Default` connection string at an in-memory SQLite database, which lives in RAM and disappears when the last connection closes. The `_keepAlive` connection stays open so the database survives for the whole test run. `EnsureCreated()` builds the tables from your model without running migrations.

Use `TodoApiFactory` in place of `WebApplicationFactory<Program>` in your test class.

```csharp [TodoApi.Tests/TodoEndpointsTests.cs]
public class TodoEndpointsTests(TodoApiFactory factory)
    : IClassFixture<TodoApiFactory>
```

::: tip
Tests in the same class share one database. Write each test so it does not depend on data another test created, or reset the data at the start of the test.
:::

## Learn more

- [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
- [Unit and integration tests in Minimal API apps](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/test-min-api)
