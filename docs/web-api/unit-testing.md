# Unit tests and mocking

## Introduction

Integration tests from the [Integration tests](/web-api/testing) page boot the whole app, which is thorough but slow once you have hundreds of them. Unit tests call one class or method directly and check its result in milliseconds. Use them for logic with branches worth testing. Thin endpoints that only load and save are better covered by integration tests.

<ApiStyle />

## Unit tests vs integration tests

A unit test checks one piece of code in isolation, replacing its slow or external dependencies with fakes. An integration test runs the real pieces together through HTTP. A healthy test suite has many unit tests for business rules and fewer integration tests that prove the wiring works. Both run with `dotnet test` from the same `TodoApi.Tests` project.

## A service to test

Extend `TodoService` from the [Dependency injection](/web-api/dependency-injection) page so completing a todo sends an email.

```csharp [Services/IEmailSender.cs]
namespace TodoApi.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}
```

If you built the [Email](/web-api/email) page, use the interface from there instead. The test below only needs a method with this shape.

```csharp [Services/TodoService.cs (excerpt)]
public class TodoService(AppDbContext db, IEmailSender email) : ITodoService
{
    public async Task<bool> CompleteAsync(int id)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return false;
        }

        if (!todo.IsDone)
        {
            todo.IsDone = true;
            await db.SaveChangesAsync();
            await email.SendAsync("owner@example.com", "Todo done", $"{todo.Title} is done.");
        }

        return true;
    }
}
```

Add `Task<bool> CompleteAsync(int id);` to `ITodoService`, and make sure an `IEmailSender` implementation is registered in `Program.cs`.

## A real database without a server

The service needs an `AppDbContext`. Use SQLite in memory, the same provider your app runs on, so constraints and SQL translation behave the way they do in production.

```csharp [TodoApi.Tests/TestDb.cs]
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Tests;

public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public AppDbContext Context { get; }

    public TestDb()
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
```

An in-memory SQLite database lives as long as its connection stays open. Each `TestDb` gets a fresh, empty database, so tests never share data.

## Mocking with NSubstitute

```bash
dotnet add TodoApi.Tests package NSubstitute
```

A mock is a fake object that records how it was called, so the test can check it. NSubstitute is the most readable mocking library for .NET. Moq and FakeItEasy do the same job.

```csharp [TodoApi.Tests/TodoServiceTests.cs]
using NSubstitute;
using TodoApi.Models;
using TodoApi.Services;
using Xunit;

namespace TodoApi.Tests;

public class TodoServiceTests
{
    [Fact]
    public async Task CompleteAsync_MarksDoneAndSendsEmail()
    {
        using var db = new TestDb();
        var todo = new Todo { Title = "Buy milk" };
        db.Context.Todos.Add(todo);
        await db.Context.SaveChangesAsync();

        var email = Substitute.For<IEmailSender>();
        var service = new TodoService(db.Context, email);

        var found = await service.CompleteAsync(todo.Id);

        Assert.True(found);
        Assert.True(todo.IsDone);
        await email.Received(1).SendAsync(
            Arg.Is("owner@example.com"), Arg.Is("Todo done"), Arg.Any<string>());
    }

    [Fact]
    public async Task CompleteAsync_ReturnsFalse_WhenTodoIsMissing()
    {
        using var db = new TestDb();
        var email = Substitute.For<IEmailSender>();
        var service = new TodoService(db.Context, email);

        var found = await service.CompleteAsync(42);

        Assert.False(found);
        await email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
```

`Substitute.For<IEmailSender>()` creates a fake that does nothing when called. `Received(1)` fails the test unless `SendAsync` was called exactly once with matching arguments. `Arg.Is` matches one exact value and `Arg.Any<string>()` matches any value. No real email is sent.

## Testing endpoints directly

<div class="only-minimal">

Handlers in `TodoEndpoints` are static methods, so a test can call them like any other method. Change the ones you want to test from `static` to `public static`.

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
public static async Task<Results<Ok<Todo>, NotFound>> GetById(int id, AppDbContext db, ILogger<Todo> logger)
```

```csharp [TodoApi.Tests/TodoHandlerTests.cs]
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApi.Endpoints;
using TodoApi.Models;
using Xunit;

namespace TodoApi.Tests;

public class TodoHandlerTests
{
    [Fact]
    public async Task GetById_ReturnsOk_WhenTodoExists()
    {
        using var db = new TestDb();
        var todo = new Todo { Title = "Walk the dog" };
        db.Context.Todos.Add(todo);
        await db.Context.SaveChangesAsync();

        var result = await TodoEndpoints.GetById(todo.Id, db.Context, NullLogger<Todo>.Instance);

        var ok = Assert.IsType<Ok<Todo>>(result.Result);
        Assert.Equal("Walk the dog", ok.Value?.Title);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenTodoIsMissing()
    {
        using var db = new TestDb();

        var result = await TodoEndpoints.GetById(99, db.Context, NullLogger<Todo>.Instance);

        Assert.IsType<NotFound>(result.Result);
    }
}
```

The `ILogger<Todo>` parameter comes from the [Logging](/web-api/logging) page. `NullLogger<Todo>.Instance` is a logger that throws every message away, which is all a test needs.

`Results<Ok<Todo>, NotFound>` exposes the actual result through its `Result` property. `Assert.IsType<Ok<Todo>>` checks which one the handler returned and gives you the typed object, so you can read `Value` without parsing JSON.

This is the payoff of named static handlers with `TypedResults`. A lambda inside `MapGet` has no name a test can call, and a handler returning `IResult` forces you to cast and guess. With typed static methods, the compiler and the test both know every possible response.

</div>

<div class="only-controllers">

A controller is a plain class. A test can create one with `new` and call its actions like any other method, with no web server involved.

```csharp [TodoApi.Tests/TodosControllerTests.cs]
using Microsoft.AspNetCore.Mvc;
using TodoApi.Controllers;
using TodoApi.Models;
using Xunit;

namespace TodoApi.Tests;

public class TodosControllerTests
{
    [Fact]
    public async Task GetById_ReturnsOk_WhenTodoExists()
    {
        using var db = new TestDb();
        var todo = new Todo { Title = "Walk the dog" };
        db.Context.Todos.Add(todo);
        await db.Context.SaveChangesAsync();

        var controller = new TodosController(db.Context);

        var result = await controller.GetById(todo.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<Todo>(ok.Value);
        Assert.Equal("Walk the dog", value.Title);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenTodoIsMissing()
    {
        using var db = new TestDb();
        var controller = new TodosController(db.Context);

        var result = await controller.GetById(99);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
```

The test passes the `TestDb` context to the constructor, the same way dependency injection does when the app runs. If your controller takes more services, pass fakes for them too: `NullLogger<TodosController>.Instance` for an `ILogger<TodosController>` from the [Logging](/web-api/logging) page, or `Substitute.For<IEmailSender>()` for an email sender.

`ActionResult<Todo>` holds what the action returned in its `Result` property. `Ok(todo)` produces an `OkObjectResult` whose `Value` is the todo, and `NotFound()` produces a `NotFoundResult`. `Assert.IsType` checks which one you got and gives you the typed object, so you can read the todo without parsing JSON.

</div>

## Avoid the EF Core InMemory provider

The `Microsoft.EntityFrameworkCore.InMemory` package looks convenient, but it is not a relational database. It ignores foreign keys and unique indexes, has no transactions, and runs LINQ in memory, so a query that fails to translate to SQL still passes the test. Microsoft recommends against it for testing. Use SQLite in memory as shown above, or a real database as on [Testing with real databases](/web-api/testcontainers).

::: tip
Some teams stop at the service layer and wrap `AppDbContext` in a repository interface so they can mock it. That adds code and still does not prove your queries work. Testing against a real SQLite database is less code and catches more bugs.
:::

## Learn more

- [Unit and integration tests in Minimal API apps](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/test-min-api)
- [Testing EF Core applications](https://learn.microsoft.com/ef/core/testing/)
