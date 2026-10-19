# Project structure

## Introduction

A new `TodoApi` project has only a handful of files. This page explains what each one does and clears out the sample code so you can start fresh.

## The files

```text
TodoApi/
├── Properties/
│   └── launchSettings.json
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
├── TodoApi.csproj
└── TodoApi.http
```

You will also see `bin/` and `obj/` folders after the first build. They hold compiled output, and you can ignore them.

## Program.cs

`Program.cs` is where your app starts. Here is the file the .NET 10 template gives you:

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
```

The file has no class or `Main` method. C# allows top-level statements, which means the code in this file runs from top to bottom when the app starts.

The file has two halves. Everything before `builder.Build()` registers services, which are objects your app can ask for later. Everything after it sets up the request pipeline and the endpoints. `AddOpenApi` and `MapOpenApi` describe your API in a standard format, covered on the [OpenAPI](/web-api/openapi) page.

## Remove the sample

Delete the weather forecast code and keep the rest:

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
```

Also delete the `GET` request in `TodoApi.http`. You will add your own `/todos` endpoints on the [Minimal APIs](/web-api/minimal-apis) page.

## TodoApi.http

This file holds sample requests you can send from your editor. The first line stores the base address in a variable so each request can reuse it.

## appsettings.json

`appsettings.json` holds settings such as log levels and connection strings. `appsettings.Development.json` overrides those settings when you run the app on your machine. The [Configuration](/web-api/configuration) page covers both.

## Properties/launchSettings.json

```json [Properties/launchSettings.json]
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "applicationUrl": "http://localhost:5123",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

This file tells `dotnet run` and `dotnet watch` which address to listen on and which environment to use. The real file has an `https` profile too. It only applies on your machine and is not used when you deploy.

## TodoApi.csproj

```xml [TodoApi.csproj]
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
  </ItemGroup>

</Project>
```

The project file lists the target framework and the NuGet packages your app uses. NuGet is the package manager for .NET. `ImplicitUsings` adds common `using` lines to every file, which is why `Program.cs` has none at the top.

## How a .NET project builds

The .NET SDK is the toolset you installed. It holds the compiler and the `dotnet` command. The .NET runtime is the smaller part that runs a finished app. Your machine needs the SDK, and a server only needs the runtime.

`TodoApi.csproj` tells the SDK what to build. `TargetFramework` picks the .NET version, here `net10.0`. Each `PackageReference` names a NuGet package and its version.

```bash
dotnet restore
dotnet build
dotnet publish -c Release
```

`dotnet restore` downloads the packages listed in the project file. `dotnet build` runs restore for you, then compiles your code into `bin/Debug/net10.0/`. The `obj/` folder holds files the build creates along the way. `dotnet run` and `dotnet watch` build before they start the app, so you rarely call `build` yourself.

`dotnet publish` makes the folder you deploy. It builds in Release mode, an optimized build without debug checks, and copies the app with everything it needs into `bin/Release/net10.0/publish/`. See the [CLI cheat sheet](/web-api/cli) for every command.

## Learn more

- [ASP.NET Core fundamentals overview](https://learn.microsoft.com/aspnet/core/fundamentals/)
- [Use multiple environments in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/environments)
