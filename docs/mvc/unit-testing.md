# Unit tests and mocking

## Introduction

Integration tests from the [Testing](/mvc/testing) page start the whole app, which makes them slow once you have hundreds. Unit tests check one class at a time in milliseconds, so you can run them on every save. Write them for classes with real logic: rules, calculations, and decisions. Skip them for code that only passes data from the database to a view, because an integration test covers that better.

## Unit or integration

A unit test creates one class directly with `new`, gives it fake versions of its dependencies, and checks what it returns or what it calls. A fake is a stand-in object that behaves the way the test needs. Unit tests catch logic bugs and run fast.

An integration test runs real routing, real middleware, and a real database. It catches wiring bugs, such as a missing service registration or a wrong route, that unit tests never see.

Use both. A good default is unit tests for services and their rules, and a few integration tests per controller for the happy path and the main failure.

## The code under test

This page extends `BookService` from [Dependency injection](/mvc/dependency-injection) so it emails the editor when a book is added. It assumes an `IEmailSender` like the one on the [Sending email](/mvc/email) page, with this shape.

```csharp [Services/IEmailSender.cs]
namespace BookShelf.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}
```

```csharp [Services/BookService.cs]
using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public class BookService(AppDbContext db, IEmailSender email) : IBookService
{
    public Task<List<Book>> GetAllAsync() =>
        db.Books.OrderBy(b => b.Title).ToListAsync();

    public async Task<Book?> FindAsync(int id) =>
        await db.Books.FindAsync(id);

    public async Task<Book> CreateAsync(Book book)
    {
        db.Books.Add(book);
        await db.SaveChangesAsync();

        await email.SendAsync("editor@bookshelf.test", "New book added",
            $"{book.Title} by {book.Author} was added.");

        return book;
    }
}
```

`CreateAsync` already exists on `IBookService` from the Dependency injection page. This version also sends the email. If you changed `BookService` on the [Caching](/mvc/caching) or [Async](/mvc/async) pages, keep your version and add only the `email` parameter and the new `CreateAsync` body.

## Setting up the test project

```bash
dotnet new xunit -n BookShelf.Tests
dotnet add BookShelf.Tests reference BookShelf
dotnet add BookShelf.Tests package NSubstitute
dotnet sln add BookShelf.Tests
```

Skip the commands you already ran on the Testing page. NSubstitute is a mocking library, which creates fake objects for you at runtime. Moq and FakeItEasy do the same job. Pick one and stay with it.

## A test with a hand-written fake

`BookService` needs an `AppDbContext`. Use SQLite in memory, which is fast and behaves like a real database.

```csharp [BookShelf.Tests/TestDb.cs]
using BookShelf.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Tests;

public static class TestDb
{
    public static AppDbContext Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
```

An in-memory SQLite database lives as long as its connection stays open, so each call gives a test its own empty database. `EnsureCreated` builds the tables from your model without running migrations. The connection stays open until the test process ends, which is fine for tests.

Now fake the email sender by hand.

```csharp [BookShelf.Tests/BookServiceTests.cs]
using BookShelf.Models;
using BookShelf.Services;
using Xunit;

namespace BookShelf.Tests;

public class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string body)
    {
        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

public class BookServiceTests
{
    [Fact]
    public async Task CreateAsync_SavesBookAndEmailsEditor()
    {
        using var db = TestDb.Create();
        var email = new FakeEmailSender();
        var service = new BookService(db, email);

        var book = await service.CreateAsync(new Book { Title = "Dune", Author = "Frank Herbert", Year = 1965 });

        Assert.NotEqual(0, book.Id);
        var sent = Assert.Single(email.Sent);
        Assert.Equal("editor@bookshelf.test", sent.To);
        Assert.Contains("Dune", sent.Body);
    }
}
```

The fake records every call in a list instead of sending mail. The test then checks that one email went out and that it mentions the book. `Assert.Single` fails if the list has zero or more than one item, and returns the item when it passes.

## Mocking with NSubstitute

A hand-written fake is clear but turns into boilerplate once you have many interfaces. NSubstitute builds the fake for you.

```csharp [BookShelf.Tests/BookServiceTests.cs (excerpt)]
using NSubstitute;

[Fact]
public async Task CreateAsync_SendsOneEmail()
{
    using var db = TestDb.Create();
    var email = Substitute.For<IEmailSender>();
    var service = new BookService(db, email);

    await service.CreateAsync(new Book { Title = "Kindred", Author = "Octavia E. Butler", Year = 1979 });

    await email.Received(1).SendAsync(
        "editor@bookshelf.test",
        Arg.Any<string>(),
        Arg.Is<string>(body => body.Contains("Kindred")));
}
```

`Substitute.For<IEmailSender>()` creates an object that implements the interface and does nothing by default. Methods that return `Task` return a completed task. `Received(1)` checks that the method was called exactly once with matching arguments, and fails the test with a clear message when it was not. `Arg.Any` matches any value, and `Arg.Is` matches values that pass a condition.

To make a fake return something, use `Returns`, for example `books.FindAsync(1).Returns(new Book { Id = 1, Title = "Dune" })`.

## Testing a controller action directly

```csharp [BookShelf.Tests/BooksControllerTests.cs]
using BookShelf.Controllers;
using BookShelf.Models;
using BookShelf.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace BookShelf.Tests;

public class BooksControllerTests
{
    [Fact]
    public async Task Details_ReturnsNotFound_WhenBookIsMissing()
    {
        var books = Substitute.For<IBookService>();
        var controller = new BooksController(books);

        var result = await controller.Details(42);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsViewWithBook()
    {
        var books = Substitute.For<IBookService>();
        books.FindAsync(1).Returns(new Book { Id = 1, Title = "Dune" });
        var controller = new BooksController(books);

        var result = await controller.Details(1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Book>(view.Model);
        Assert.Equal("Dune", model.Title);
    }
    // If your BooksController takes more constructor parameters,
    // pass Substitute.For<T>() or NullLogger<T>.Instance for each one.
}
```

This uses the `BooksController(IBookService books)` version from the Dependency injection page. An unconfigured substitute returns `null` from `FindAsync`, which is what the first test needs. `Assert.IsType` checks the exact result type and casts it, so you can inspect the view model.

These tests do not render the view or run filters such as `[ValidateAntiForgeryToken]`. Test those with an integration test.

## Keeping tests fast

Unit tests should run in milliseconds and give the same result every time. Never call real email servers, HTTP APIs, or `Task.Delay` in them. If a class reads the current time, inject `TimeProvider` and use `FakeTimeProvider` from the `Microsoft.Extensions.TimeProvider.Testing` package in tests. Run `dotnet test` often, and keep slow tests in the integration project.

::: warning
Avoid the EF Core InMemory provider (`UseInMemoryDatabase`). It is not a relational database, so it ignores foreign keys, unique indexes, and transactions, and some LINQ queries that fail on a real database pass on it. Use SQLite in memory as shown above, or a real database as on the [Testing with real databases](/mvc/testcontainers) page.
:::

## Learn more

- [Unit testing C# with xUnit](https://learn.microsoft.com/dotnet/core/testing/unit-testing-csharp-with-xunit)
- [Choosing a testing strategy for EF Core](https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy)
