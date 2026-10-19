# Configuration

## Introduction

Configuration is any setting you want to change without editing code, such as a page size or a connection string. ASP.NET Core reads it from JSON files, environment variables, and a few other places, then merges it into one set of values.

<ApiStyle />

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
  "Todo": {
    "MaxItems": 100,
    "DefaultTitle": "New todo"
  }
}
```

The template creates the `Logging` and `AllowedHosts` sections. The `Todo` section is one you add yourself. Settings are grouped into sections, and each key inside a section has a value.

## Environments

```json [appsettings.Development.json]
{
  "Todo": {
    "MaxItems": 5
  }
}
```

ASP.NET Core loads `appsettings.json` first, then `appsettings.{Environment}.json` on top of it. Values in the second file win. Here `MaxItems` becomes `5` in development and stays `100` everywhere else.

The environment name comes from the `ASPNETCORE_ENVIRONMENT` variable. `launchSettings.json` sets it to `Development` on your machine. When the variable is missing, the app runs as `Production`. You can check the environment in code:

```csharp [Program.cs]
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

## Reading values

<div class="only-minimal">

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

var defaultTitle = builder.Configuration["Todo:DefaultTitle"];
var maxItems = builder.Configuration.GetValue<int>("Todo:MaxItems");

var app = builder.Build();

app.MapGet("/settings", () => new { defaultTitle, maxItems });

app.Run();
```

`builder.Configuration` holds every setting. A colon separates a section from its key, so `Todo:MaxItems` reads `MaxItems` inside `Todo`. The indexer returns a string, and `GetValue<T>` converts it to the type you ask for.

</div>

<div class="only-controllers">

```csharp [Controllers/SettingsController.cs]
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Route("settings")]
public class SettingsController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var defaultTitle = configuration["Todo:DefaultTitle"];
        var maxItems = configuration.GetValue<int>("Todo:MaxItems");

        return Ok(new { defaultTitle, maxItems });
    }
}
```

`IConfiguration` holds every setting. It is the same object as `builder.Configuration` in `Program.cs`, and any controller can ask for it in its constructor. A colon separates a section from its key, so `Todo:MaxItems` reads `MaxItems` inside `Todo`. The indexer returns a string, and `GetValue<T>` converts it to the type you ask for. This assumes `Program.cs` calls `AddControllers` and `MapControllers`, as shown on [CRUD](/web-api/crud).

</div>

## The options pattern

Reading strings by key gets messy as settings grow. The options pattern binds a section to a class instead:

```csharp [TodoSettings.cs]
namespace TodoApi;

public class TodoSettings
{
    public int MaxItems { get; set; }
    public string DefaultTitle { get; set; } = "";
}
```

<div class="only-minimal">

```csharp [Program.cs]
using Microsoft.Extensions.Options;
using TodoApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TodoSettings>(
    builder.Configuration.GetSection("Todo"));

var app = builder.Build();

app.MapGet("/settings", (IOptions<TodoSettings> options) => options.Value);

app.Run();
```

</div>

<div class="only-controllers">

```csharp [Program.cs]
using TodoApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TodoSettings>(
    builder.Configuration.GetSection("Todo"));
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
```

```csharp [Controllers/SettingsController.cs]
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace TodoApi.Controllers;

[ApiController]
[Route("settings")]
public class SettingsController(IOptions<TodoSettings> options) : ControllerBase
{
    [HttpGet]
    public TodoSettings Get() => options.Value;
}
```

</div>

`Configure<TodoSettings>` copies the `Todo` section into a `TodoSettings` object. Any endpoint or class can then ask for `IOptions<TodoSettings>` and read `.Value`. Property names must match the JSON keys, but case does not matter. The [Dependency injection](/web-api/dependency-injection) page explains how asking for a type in a parameter works.

## User secrets

Passwords and API keys should never go in `appsettings.json`, because that file is committed to source control. In development, store them with user secrets:

```bash
dotnet user-secrets init
dotnet user-secrets set "Todo:ApiKey" "my-secret-key"
```

The first command adds an ID to your `.csproj`. The second saves the value in a file in your user profile, outside the project folder. In the Development environment ASP.NET Core loads it automatically, so `builder.Configuration["Todo:ApiKey"]` returns it.

::: warning
User secrets are for development only. They are not encrypted. In production, use environment variables or a secret store from your hosting provider.
:::

Environment variables also override JSON values. Use a double underscore in place of the colon, for example `Todo__MaxItems=50`.

## Learn more

- [Configuration in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)
- [Options pattern in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options)
