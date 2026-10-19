# Browser tests

## Introduction

Browser tests drive a real browser through your app: they open pages, fill in forms, click buttons, and check what appears on screen. They catch problems that [integration tests](/mvc/testing) miss, such as broken JavaScript or a button that htmx never wires up. This page uses Microsoft.Playwright with xUnit to add a book through the form and check it shows up in the list.

Browser tests are slower than integration tests. Keep a few for the paths that matter most, and test the rest with `WebApplicationFactory` and `HttpClient`.

## Creating the test project

Run these commands from the folder that contains your `BookShelf` project folder.

```bash
dotnet new xunit -n BookShelf.BrowserTests
dotnet sln add BookShelf.BrowserTests/BookShelf.BrowserTests.csproj
dotnet add BookShelf.BrowserTests reference BookShelf/BookShelf.csproj
dotnet add BookShelf.BrowserTests package Microsoft.Playwright
dotnet add BookShelf.BrowserTests package Microsoft.AspNetCore.Mvc.Testing
dotnet build
```

`Microsoft.Playwright` is the .NET library that controls Chromium, Firefox, and WebKit. `Microsoft.AspNetCore.Mvc.Testing` starts your app from the test, as on the [Testing](/mvc/testing) page. The app needs `public partial class Program { }` at the end of `Program.cs`, which that page adds.

::: warning
The Playwright package copies many files into `bin`, some with long paths. On Windows, if the build fails with "exceeds the OS max path limit", move the solution to a shorter folder such as `C:\src\BookShelf`.
:::

## Installing browsers

The build creates a script that downloads the browsers Playwright uses:

```bash
pwsh BookShelf.BrowserTests/bin/Debug/net10.0/playwright.ps1 install
```

`pwsh` is PowerShell 7. It runs on Windows, macOS, and Linux. On Windows without PowerShell 7, use `powershell` in its place. In CI on Linux, add `--with-deps` to also install the system libraries the browsers need.

If you only need Chromium-based tests on a Windows machine, you can skip the download and use the Microsoft Edge that is already installed. The test below does this with `Channel = "msedge"`.

## Running the app on a real port

A browser cannot talk to the in-memory test server that `WebApplicationFactory` normally uses. In .NET 10, `UseKestrel` makes the factory start the app on a real port instead.

```csharp [BookShelf.BrowserTests/BookShelfAppFactory.cs]
using BookShelf.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BookShelf.BrowserTests;

public class BookShelfAppFactory : WebApplicationFactory<Program>
{
    public BookShelfAppFactory()
    {
        UseKestrel(0);   // listen on a real port; 0 picks a free one
        StartServer();
    }

    public string BaseUrl => ClientOptions.BaseAddress.ToString().TrimEnd('/');

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", "Data Source=browser-tests.db");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        return host;
    }
}
```

`UseKestrel(0)` swaps the test server for Kestrel, the real web server. Port `0` asks the operating system for a free port, so tests never clash with an app you have running. `StartServer()` starts it right away, and `ClientOptions.BaseAddress` then holds the address, including the port that was picked.

The factory points the app at its own SQLite file, `browser-tests.db`, and recreates it on each run. `EnsureCreated` builds the tables from your model without running migrations, which is fine for a throwaway test database.

## Writing the test

```csharp [BookShelf.BrowserTests/BooksPageTests.cs]
using Microsoft.Playwright;

namespace BookShelf.BrowserTests;

public class BooksPageTests(BookShelfAppFactory app) : IClassFixture<BookShelfAppFactory>, IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Channel = "msedge" });
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Adding_a_book_shows_it_in_the_list()
    {
        var page = await _browser.NewPageAsync();
        var title = $"Test book {Guid.NewGuid():N}";

        await page.GotoAsync($"{app.BaseUrl}/Books");
        await page.GetByRole(AriaRole.Link, new() { Name = "Add a book" }).ClickAsync();

        await page.GetByLabel("Title").FillAsync(title);
        await page.GetByLabel("Author").FillAsync("Ada Writer");
        await page.GetByLabel("Year").FillAsync("2024");
        await page.GetByLabel("Price").FillAsync("12.50");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await page.WaitForURLAsync($"{app.BaseUrl}/Books");
        await Assertions.Expect(page.Locator("#book-rows")).ToContainTextAsync(title);
    }
}
```

`IClassFixture<BookShelfAppFactory>` starts the app once for every test in the class. `IAsyncLifetime` gives xUnit an async setup and teardown, where the test launches and closes the browser. Remove `Channel = "msedge"` to use the Chromium that `playwright.ps1 install` downloaded.

The test finds elements the way a user would. `GetByRole` finds a link or button by its visible text, and `GetByLabel` finds the input that a `<label asp-for="...">` points to. A unique title means the check cannot pass by accident on leftover data.

`Assertions.Expect(...).ToContainTextAsync` waits, by default up to 5 seconds, for the text to appear. You rarely need manual waits or `Task.Delay` with Playwright.

## Running the tests

```bash
dotnet test
```

Browsers run headless by default, so no window opens. To watch the test, launch with `new() { Channel = "msedge", Headless = false, SlowMo = 500 }`. `SlowMo` pauses 500 milliseconds between actions.

::: tip
Set the environment variable `PWDEBUG=1` before `dotnet test` to open the Playwright Inspector. It pauses the test so you can step through each action.
:::

## Learn more

- [Testing](/mvc/testing)
- [Interactive pages with htmx](/mvc/htmx)
- [Playwright for .NET](https://playwright.dev/dotnet/docs/intro)
- [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
