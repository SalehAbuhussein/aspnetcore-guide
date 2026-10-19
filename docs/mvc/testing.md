# Testing

## Introduction

Integration tests start your real app in memory and send it HTTP requests, the same way a browser would. This page sets up an xUnit test project, checks that the book list loads, posts a form with its antiforgery token, and runs every test against a fresh in-memory database.

## Creating the test project

Run these commands from the folder that contains your `BookShelf` project folder.

```bash
dotnet new sln -n BookShelf
dotnet new xunit -n BookShelf.Tests
dotnet sln add BookShelf/BookShelf.csproj BookShelf.Tests/BookShelf.Tests.csproj
dotnet add BookShelf.Tests reference BookShelf/BookShelf.csproj
dotnet add BookShelf.Tests package Microsoft.AspNetCore.Mvc.Testing
```

A solution is a file that groups several projects so you can build and test them together. In .NET 10, `dotnet new sln` creates a `BookShelf.slnx` file. xUnit is the most common .NET test framework. `Microsoft.AspNetCore.Mvc.Testing` gives you `WebApplicationFactory`, which starts the app in memory.

Your folders now look like this:

```text
BookShelf.slnx
BookShelf/
└── BookShelf.csproj
BookShelf.Tests/
└── BookShelf.Tests.csproj
```

## Using an in-memory database

Tests should not touch your real `bookshelf.db`. Create a factory that points the app at an in-memory SQLite database instead.

```csharp [BookShelf.Tests/BookShelfFactory.cs]
using BookShelf.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BookShelf.Tests;

public class BookShelfFactory : WebApplicationFactory<Program>
{
    private const string ConnectionString = "Data Source=BookShelfTests;Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive = new(ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);

        // Let tests reach [Authorize] actions without signing in.
        builder.ConfigureTestServices(services =>
            services.AddAuthorization(options =>
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAssertion(_ => true)
                    .Build()));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _keepAlive.Dispose();
    }
}
```

`UseSetting` overrides the `Default` connection string before `Program.cs` reads it. `ConfigureTestServices` runs after your own service setup, and here it swaps the default authorization policy for one that always passes. That lets tests reach the actions you protected with `[Authorize]` on the [Authorization](/mvc/authorization) page. `Mode=Memory` keeps the database in RAM, and `Cache=Shared` lets every connection with the same name see the same data.

An in-memory SQLite database is deleted when its last connection closes. EF Core opens and closes connections all the time, so `_keepAlive` holds one open for the life of the factory. `EnsureCreated` builds the tables from your model, including the `HasData` rows.

::: tip
Each factory gets the database named in `Data Source`. If two test classes must not share data, give each its own name, for example by adding a `Guid` to the name.
:::

## The first test

```csharp [BookShelf.Tests/BooksPageTests.cs]
namespace BookShelf.Tests;

public class BooksPageTests(BookShelfFactory factory)
    : IClassFixture<BookShelfFactory>
{
    [Fact]
    public async Task Index_returns_the_book_list()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/Books");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The Hobbit", html);
    }
}
```

```bash
dotnet test
```

`BookShelfFactory` is the `WebApplicationFactory<Program>` from the previous section. It builds your app from its `Program.cs` and runs it in memory, with no real network port. `Program` is the class the compiler makes from your top-level statements. In .NET 10 it is public automatically, so the test project can see it without any extra code.

`IClassFixture` tells xUnit to create one factory and share it with every test in the class, so the app starts once. `CreateClient` returns an `HttpClient` wired to the in-memory server. `EnsureSuccessStatusCode` fails the test unless the status is 2xx, such as 200 OK. "The Hobbit" comes from the `HasData` seed on the [Seeding](/mvc/seeding) page.

## Posting a form with the antiforgery token

POST actions have `[ValidateAntiForgeryToken]`, so a test that posts without the token gets 400 Bad Request. Read the token from the GET page first, the same way a browser does.

```csharp [BookShelf.Tests/Antiforgery.cs]
using System.Text.RegularExpressions;

namespace BookShelf.Tests;

public static partial class Antiforgery
{
    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>")]
    private static partial Regex TokenInputRegex();

    [GeneratedRegex("value=\"([^\"]+)\"")]
    private static partial Regex ValueRegex();

    public static async Task<string> GetTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var input = TokenInputRegex().Match(html);
        var value = ValueRegex().Match(input.Value);
        if (!input.Success || !value.Success)
            throw new InvalidOperationException($"No antiforgery token on {url}.");
        return value.Groups[1].Value;
    }
}
```

The form tag helper writes the token as a hidden input named `__RequestVerificationToken`. The first pattern finds that `<input>` tag, and the second reads its `value`, so the order of the attributes does not matter. The GET response also sets an antiforgery cookie, and the client from `CreateClient` stores cookies and sends them back, so the token and cookie match.

```csharp [BookShelf.Tests/BooksPageTests.cs (excerpt)]
[Fact]
public async Task Create_adds_a_book()
{
    var client = factory.CreateClient();
    var token = await Antiforgery.GetTokenAsync(client, "/Books/Create");

    var response = await client.PostAsync("/Books/Create", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token,
        ["Title"] = "Piranesi",
        ["Author"] = "Susanna Clarke",
        ["Year"] = "2020"
    }));

    response.EnsureSuccessStatusCode();
    Assert.Contains("Piranesi", await response.Content.ReadAsStringAsync());
}
```

`FormUrlEncodedContent` sends the fields the way an HTML form does. The action redirects to `/Books`, and the client follows redirects by default, so the final response is the book list.

::: warning
The always-pass policy only covers `[Authorize]` with no role or policy. Actions with `[Authorize(Roles = "Admin")]` or a named policy still reject the test. To test those, sign in a fake user with a test authentication handler, which the Microsoft docs below describe.
:::

For testing a single class without starting the app, see [Unit testing](/mvc/unit-testing).

## Learn more

- [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
- [Unit testing C# with xUnit](https://learn.microsoft.com/dotnet/core/testing/unit-testing-csharp-with-xunit)
