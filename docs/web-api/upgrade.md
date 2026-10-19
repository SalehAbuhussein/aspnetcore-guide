# Upgrade guide

## Introduction

This page moves an API from .NET 8 or .NET 9 to .NET 10. For a small app like `TodoApi` it takes 15 to 30 minutes: change one line in the project file, update packages, fix any warnings, and run your tests.

Install the .NET 10 SDK first. The [Installation](/web-api/installation) page shows how. Check it with:

```bash
dotnet --version
```

The output should start with `10.0`.

## 1. Change the target framework

Open `TodoApi.csproj` and change `TargetFramework`:

```xml [TodoApi.csproj]
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

If your solution has more than one project, such as a test project, change every one of them. C# 14 comes with `net10.0`, so you do not need a `LangVersion` setting.

## 2. Update global.json

If the repository has a `global.json` file, it pins the SDK version. Point it at .NET 10 or the build keeps using the old SDK, or fails if that SDK is missing.

```json [global.json]
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

`latestFeature` lets the build use any newer 10.0 SDK on the machine, such as `10.0.401`. If you do not have a `global.json`, skip this step.

## 3. Update packages

List the packages that have newer versions:

```bash
dotnet list package --outdated
```

Update each Microsoft package to its `10.0.x` version. The major version of `Microsoft.EntityFrameworkCore.*`, `Microsoft.AspNetCore.*`, and `Microsoft.Extensions.*` packages should match the target framework.

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
```

`dotnet add package` without `--version` installs the newest stable version. Third-party packages follow their own versions, so check their release notes.

The `dotnet-outdated` tool is an optional helper that updates everything in one go:

```bash
dotnet tool install -g dotnet-outdated-tool
dotnet outdated --upgrade
```

Update the EF Core command-line tool too, so it matches EF Core 10:

```bash
dotnet tool update -g dotnet-ef
```

## 4. Build and read the warnings

```bash
dotnet build
```

Most upgrades build on the first try. If you have OpenAPI transformers, expect a few errors there. Read the warnings: APIs that are now obsolete show up as warnings, often with an ID such as `ASPDEPR` or `SYSLIB`, before they are removed in a later version.

## Breaking changes to know about

These are the ASP.NET Core 10 changes most likely to affect an API:

| Change | What to do |
| --- | --- |
| Cookie authentication no longer redirects API endpoints to a login page. Unauthenticated calls get `401` and forbidden calls get `403`. | Usually nothing: this is what API clients want. If a client expected a `302`, update it. See [Authentication](/web-api/authentication). |
| The built-in OpenAPI generator moved to `Microsoft.OpenApi` 2.x and OpenAPI 3.1. Types such as `OpenApiSchema` changed, and transformers that used them may not compile. | Fix the transformer code against the new types. See [OpenAPI](/web-api/openapi). |
| `WithOpenApi()` on Minimal API endpoints is obsolete. | Remove it. Use `WithSummary`, `WithDescription`, or an operation transformer. |
| `IActionContextAccessor` and `ActionContextAccessor` are obsolete. | Use `IHttpContextAccessor`. |
| `WebHostBuilder` and `IWebHost` are obsolete. | Use `WebApplication.CreateBuilder`, which every template has used since .NET 6. |
| The exception handler middleware no longer logs an exception that an `IExceptionHandler` reports as handled. | If you relied on that log entry, log inside your handler. See [Errors](/web-api/errors). |

The full list, including runtime and SDK changes, is on [Breaking changes in .NET 10](https://learn.microsoft.com/dotnet/core/compatibility/10.0).

## Moving from Swashbuckle to built-in OpenAPI

Templates since .NET 9 use the built-in `Microsoft.AspNetCore.OpenApi` package instead of `Swashbuckle.AspNetCore`. Swashbuckle still works on .NET 10, so you do not have to switch during the upgrade. If you want to:

```bash
dotnet remove package Swashbuckle.AspNetCore
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Scalar.AspNetCore
```

```csharp [Program.cs (excerpt)]
// remove: builder.Services.AddEndpointsApiExplorer();
// remove: builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

// ...

if (app.Environment.IsDevelopment())
{
    // remove: app.UseSwagger(); app.UseSwaggerUI();
    app.MapOpenApi();
    app.MapScalarApiReference();
}
```

The document moves from `/swagger/v1/swagger.json` to `/openapi/v1.json`, and the UI moves to `/scalar`. Update any client generator or bookmark that used the old URL. Swashbuckle filters (`IOperationFilter`, `ISchemaFilter`) become transformers. See [OpenAPI](/web-api/openapi).

## Check your migrations

EF Core 10 can produce a different model snapshot. After the package update, check that the model has not changed:

```bash
dotnet ef migrations add CheckUpgrade
```

If the new migration's `Up` method is empty, delete it with `dotnet ef migrations remove`. If it has changes, read them before you apply them. See [Migrations](/web-api/migrations).

## Test checklist

1. `dotnet build` finishes with no errors and you have read every new warning.
2. `dotnet test` passes. See [Testing](/web-api/testing).
3. `dotnet ef migrations add CheckUpgrade` produces an empty migration, or one you understand.
4. The app starts with `dotnet run` and every request in your `.http` file returns the status code it did before.
5. A protected endpoint returns `401` without a token and `200` with one.
6. `/openapi/v1.json` loads and your client code generator still accepts it (it is OpenAPI 3.1 now).
7. Your CI pipeline and Dockerfile use the .NET 10 SDK and runtime images, for example `mcr.microsoft.com/dotnet/sdk:10.0`. See [CI/CD](/web-api/ci-cd) and [Deployment](/web-api/deployment).

## Learn more

- [What's new in .NET 10](/web-api/whats-new) for the features you can use after upgrading.
- [Migrate from ASP.NET Core 9.0 to 10.0](https://learn.microsoft.com/aspnet/core/migration/90-to-100) on Microsoft Learn.
