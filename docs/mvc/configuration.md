# Configuration

## Introduction

Configuration is the set of values your app reads at startup, such as a site name or a database connection string. ASP.NET Core loads these values from JSON files, environment variables, and other sources, then gives you one place to read them.

## appsettings.json

```json [appsettings.json]
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Shelf": {
    "Name": "My BookShelf",
    "PageSize": 10
  }
}
```

The template creates `appsettings.json` with the `Logging` and `AllowedHosts` sections. The `Shelf` section is new. You can add any section you like. Nested sections are joined with a colon when you read them, so the name is `Shelf:Name`.

## Environments

An environment is a name that tells the app where it is running. The three common names are `Development`, `Staging`, and `Production`. ASP.NET Core reads the name from the `ASPNETCORE_ENVIRONMENT` environment variable. If the variable is not set, the app assumes `Production`.

During development, `Properties/launchSettings.json` sets it for you:

```json [Properties/launchSettings.json]
"environmentVariables": {
  "ASPNETCORE_ENVIRONMENT": "Development"
}
```

The app loads `appsettings.json` first, then `appsettings.{Environment}.json` on top. Values in the second file win. So `appsettings.Development.json` is the place for settings you only want on your own machine:

```json [appsettings.Development.json]
{
  "Shelf": {
    "Name": "BookShelf (dev)"
  }
}
```

`PageSize` still comes from `appsettings.json`, because the development file does not override it. Environment variables load after both files and override them too. The variable `Shelf__Name` sets `Shelf:Name`. A double underscore stands in for the colon.

You can check the environment in code with `app.Environment.IsDevelopment()`. The template uses this to show detailed errors only in development.

## Read a value

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

string? shelfName = builder.Configuration["Shelf:Name"];
int pageSize = builder.Configuration.GetValue<int>("Shelf:PageSize");
```

`builder.Configuration` holds every loaded value. The indexer returns a string, or `null` if the key does not exist. `GetValue<T>` converts the value to the type you ask for.

## The options pattern

Reading strings by key works, but typos in key names fail quietly. The options pattern maps a section to a C# class instead.

```csharp [ShelfOptions.cs]
namespace BookShelf;

public class ShelfOptions
{
    public string Name { get; set; } = "";
    public int PageSize { get; set; } = 10;
}
```

Register it in `Program.cs`, before `builder.Build()`. The `using` line goes at the top of the file, because `ShelfOptions` lives in the `BookShelf` namespace:

```csharp [Program.cs]
using BookShelf;

builder.Services.Configure<ShelfOptions>(
    builder.Configuration.GetSection("Shelf"));
```

Then ask for `IOptions<ShelfOptions>` in a controller constructor:

```csharp [Controllers/HomeController.cs]
using System.Diagnostics;
using BookShelf.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BookShelf.Controllers;

public class HomeController(IOptions<ShelfOptions> options) : Controller
{
    public IActionResult Index()
    {
        ViewData["ShelfName"] = options.Value.Name;
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
```

The framework fills the class from the `Shelf` section and passes it in. This is dependency injection, covered on the [Dependency injection](/mvc/dependency-injection) page. The parentheses after the class name are a primary constructor, a short way to declare constructor parameters. `Privacy` and `Error` are the template's actions, unchanged.

## User secrets

Passwords and API keys should never go in `appsettings.json`, because that file is committed to source control. During development, store them with the Secret Manager:

```bash
dotnet user-secrets init
dotnet user-secrets set "Shelf:ApiKey" "abc123"
dotnet user-secrets list
```

`init` adds a `UserSecretsId` to your project file. `set` stores the value in a JSON file in your user profile, outside the project folder. When the environment is `Development`, the app loads user secrets automatically, and you read them like any other value: `builder.Configuration["Shelf:ApiKey"]`.

::: warning
User secrets are not encrypted and only load in `Development`. In production, use environment variables or a secret store such as Azure Key Vault. See [Deployment](/mvc/deployment).
:::

## Learn more

- [Configuration in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)
- [Safe storage of app secrets in development](https://learn.microsoft.com/aspnet/core/security/app-secrets)
