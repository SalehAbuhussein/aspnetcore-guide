# Installation

## Introduction

Before you write any code, you need the .NET SDK and a new project. This page installs both and runs the API for the first time.

## Install the .NET SDK

The SDK, or software development kit, contains the compiler, the runtime, and the `dotnet` command line tool. Download the .NET 10 SDK from [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) and run the installer for your operating system.

Check that it worked:

```bash
dotnet --version
```

The output should start with `10.`. If you see an older number, close and reopen your terminal, then try again.

## Create the project

```bash
dotnet new webapi -n TodoApi
```

`dotnet new` creates a project from a template. The `webapi` template sets up an API that uses Minimal APIs, and `-n TodoApi` names the project and its folder.

If you would rather start with controllers, add the `--use-controllers` flag:

```bash
dotnet new webapi -n TodoApi --use-controllers
```

This guide uses the Minimal API version. The [Controllers](/web-api/controllers) page shows how to add controllers to it later.

## Run the API

```bash
cd TodoApi
dotnet watch
```

`dotnet watch` builds and starts the app, then restarts it whenever you save a file. The terminal prints the address the app listens on, such as `http://localhost:5123`. Your port number will be different. Press `Ctrl+C` to stop it.

## Trust the development certificate

ASP.NET Core serves HTTPS in development with a self-signed certificate. Your browser and tools will warn about it until you trust it:

```bash
dotnet dev-certs https --trust
```

On Windows and macOS this shows a prompt you need to accept. On Linux, follow the steps the command prints, or use the plain `http` address while you learn.

## Try the API

The template includes a sample endpoint at `/weatherforecast`. It also creates a file named `TodoApi.http`:

```http [TodoApi.http]
@TodoApi_HostAddress = http://localhost:5123

GET {{TodoApi_HostAddress}}/weatherforecast/
Accept: application/json

###
```

An `.http` file holds HTTP requests you can send from your editor. In Visual Studio or in VS Code with the REST Client extension, a "Send Request" link appears above each request. Make sure the port in the first line matches the one your app printed.

You can also use `curl` from a second terminal:

```bash
curl http://localhost:5123/weatherforecast
```

Either way, you get back a JSON array of five forecasts. You will remove that sample on the [Project structure](/web-api/project-structure) page.

::: tip
Keep `dotnet watch` running in one terminal while you work through this guide. Most changes show up as soon as you save.
:::

## Learn more

- [Install .NET on Windows, Linux, and macOS](https://learn.microsoft.com/dotnet/core/install/)
- [Use .http files in Visual Studio](https://learn.microsoft.com/aspnet/core/test/http-files)
