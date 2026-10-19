# Project structure

## Introduction

The `mvc` template creates a small but complete app. This page explains what each file and folder does, so you know where to put new code.

## The folder layout

```text
BookShelf/
├── Controllers/
│   └── HomeController.cs
├── Models/
│   └── ErrorViewModel.cs
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml
│   │   └── Privacy.cshtml
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   ├── _ValidationScriptsPartial.cshtml
│   │   └── Error.cshtml
│   ├── _ViewImports.cshtml
│   └── _ViewStart.cshtml
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── lib/
├── Properties/
│   └── launchSettings.json
├── appsettings.json
├── appsettings.Development.json
├── BookShelf.csproj
└── Program.cs
```

## Program.cs

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
```

`Program.cs` is where the app starts. It has no class or `Main` method. C# allows these top-level statements in one file per project.

The file has two halves. Before `builder.Build()`, you register services. A service is an object the framework creates for you and hands to the classes that need it. `AddControllersWithViews()` registers everything MVC needs. The [Dependency injection](/mvc/dependency-injection) page covers services in depth.

After `builder.Build()`, you set up the middleware pipeline. Middleware is code that runs on every request, in order, such as redirecting HTTP to HTTPS. The [Middleware](/mvc/middleware) page explains each line. The last part, `MapControllerRoute`, tells the app how to turn URLs into controller calls. See [Routing](/mvc/routing).

## Controllers, Models, and Views

`Controllers/` holds your controller classes, such as `HomeController`. `Models/` holds the classes that carry data. `Views/` holds Razor templates, one subfolder per controller. `Views/Home/Index.cshtml` is the view for the `Index` action of `HomeController`.

`Views/Shared/` holds views that any controller can use. The most important is `_Layout.cshtml`, the page shell with the header, navigation, and footer that wraps every page. See [Layouts and partials](/mvc/layouts).

The two files that start with an underscore at the root of `Views/` apply to every view. `_ViewImports.cshtml` adds `using` lines and tag helpers. `_ViewStart.cshtml` sets the default layout.

## wwwroot

`wwwroot/` is the web root. Files in it, such as CSS, JavaScript, and images, are served to the browser as they are. `wwwroot/lib/` contains Bootstrap and jQuery. Nothing outside `wwwroot` is reachable by URL. See [Static files](/mvc/static-files).

## Settings files

`appsettings.json` stores settings such as log levels and connection strings. `appsettings.Development.json` overrides those values when you run on your own machine. The [Configuration](/mvc/configuration) page covers both.

`Properties/launchSettings.json` is only used during development. It sets the ports and the environment name that `dotnet run` and `dotnet watch` use. It is not part of the published app.

## BookShelf.csproj

```xml [BookShelf.csproj]
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

The project file tells the SDK how to build the app. `net10.0` targets .NET 10. `Nullable` turns on warnings when a value might be `null`. `ImplicitUsings` adds common `using` lines for you, which is why `Program.cs` has none. When you add NuGet packages, which are shared libraries, they appear here as `PackageReference` lines.

::: tip
The `bin/` and `obj/` folders appear after your first build. They hold compiled output. Do not edit them, and leave them out of source control.
:::

## How a .NET project builds

The .NET SDK is the toolset you installed. It holds the compiler and the `dotnet` command. The .NET runtime is the smaller part that runs a finished app. Your machine needs the SDK, and a server only needs the runtime.

`BookShelf.csproj` tells the SDK what to build. `TargetFramework` picks the .NET version, here `net10.0`. Each `PackageReference` names a NuGet package and its version.

```bash
dotnet restore
dotnet build
dotnet publish -c Release
```

`dotnet restore` downloads the packages listed in the project file. `dotnet build` runs restore for you, then compiles your code into `bin/Debug/net10.0/`. The `obj/` folder holds files the build creates along the way. `dotnet run` and `dotnet watch` build before they start the app, so you rarely call `build` yourself.

`dotnet publish` makes the folder you deploy. It builds in Release mode, an optimized build without debug checks, and copies the app with everything it needs into `bin/Release/net10.0/publish/`. See the [CLI cheat sheet](/mvc/cli) for every command.

## Learn more

- [Get started with ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/tutorials/first-mvc-app/start-mvc)
- [ASP.NET Core fundamentals overview](https://learn.microsoft.com/aspnet/core/fundamentals/)
