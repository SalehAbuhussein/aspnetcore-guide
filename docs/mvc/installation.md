# Installation

## Introduction

To build an ASP.NET Core app you need the .NET SDK and a code editor. This page installs both, creates the BookShelf project, and runs it in your browser.

## Install the .NET SDK

The .NET SDK (software development kit) contains the compiler, the runtime, and the `dotnet` command line tool. Download the .NET 10 SDK from [dot.net/download](https://dotnet.microsoft.com/download) and run the installer for your operating system.

When it finishes, open a new terminal and check the version:

```bash
dotnet --version
```

You should see a number that starts with `10.`, such as `10.0.100`. If the command is not found, close and reopen your terminal so it picks up the new path.

::: tip
.NET 10 is a long-term support (LTS) release. Microsoft supports it with security fixes for three years, which makes it a safe choice for new projects.
:::

## Create the project

```bash
dotnet new mvc -n BookShelf
cd BookShelf
```

`dotnet new` creates a project from a template. The `mvc` template gives you a working MVC app with a home page, a layout, and Bootstrap for styling. The `-n BookShelf` option names the project and the folder it lives in. The project name also becomes the root namespace, so your classes start with `BookShelf`.

## Run the app

```bash
dotnet watch
```

`dotnet watch` builds the app, starts a web server, and opens your browser. It also watches your files. When you save a change to a C# file or a view, it reloads the app for you. This is called hot reload.

The terminal prints the address the app listens on:

```text
Now listening on: https://localhost:7123
Now listening on: http://localhost:5123
```

Your port numbers will be different. Press `Ctrl+C` in the terminal to stop the app.

If you want to run the app once without watching for changes, use `dotnet run` instead.

## Trust the development certificate

The app uses HTTPS, even on your own machine. The SDK ships with a development certificate for `localhost`, but your browser will warn about it until you trust it:

```bash
dotnet dev-certs https --trust
```

On Windows and macOS this shows a prompt asking you to confirm. Accept it, then restart your browser.

::: warning
On Linux, `--trust` does not work for every browser and distribution. If you still see a warning, follow the Linux steps in the Microsoft guide linked below.
:::

## Pick an editor

Any editor works, but these three understand C# and Razor:

- Visual Studio Code with the C# Dev Kit extension. Free, and runs on Windows, macOS, and Linux.
- Visual Studio 2026 on Windows. Install the "ASP.NET and web development" workload. It includes the SDK.
- JetBrains Rider on Windows, macOS, and Linux. Free for non-commercial use.

In VS Code, open the `BookShelf` folder with File, Open Folder. C# Dev Kit detects the project and adds IntelliSense, which is the editor's code completion, plus a debugger.

## Next steps

You now have a running app. The [Project structure](/mvc/project-structure) page walks through each file the template created.

## Learn more

- [Get started with ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/tutorials/first-mvc-app/start-mvc)
- [Enforce HTTPS in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl)
