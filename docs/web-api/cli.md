# CLI cheat sheet

## Introduction

The `dotnet` commands you use most while building `TodoApi`, grouped by task. Every command here was checked against .NET SDK 10.0.401. Run them from the folder that holds `TodoApi.csproj` unless the table says otherwise.

Any command accepts `--help`, for example `dotnet ef migrations add --help`.

## Projects

| Command | What it does |
| --- | --- |
| `dotnet new webapi -n TodoApi` | Create a Minimal API project in a new `TodoApi` folder. |
| `dotnet new webapi -n TodoApi --use-controllers` | Create the same project with controllers instead. |
| `dotnet new xunit -n TodoApi.Tests` | Create an xUnit test project. |
| `dotnet new sln -n TodoApi` | Create a solution file. On SDK 10 this is `TodoApi.slnx`. |
| `dotnet sln add TodoApi/TodoApi.csproj` | Add a project to the solution in the current folder. |
| `dotnet sln list` | List the projects in the solution. |
| `dotnet sln migrate` | Create a `.slnx` file from an old `.sln` file. |
| `dotnet add TodoApi.Tests reference TodoApi` | Let the test project use the app's code. |
| `dotnet new list` | Show every installed template. |

## Running

| Command | What it does |
| --- | --- |
| `dotnet run` | Build and start the app with the first profile in `Properties/launchSettings.json`. |
| `dotnet run --launch-profile https` | Start with the `https` profile, which listens on HTTPS too. |
| `dotnet run --urls http://localhost:5050` | Listen on a different port for this run. |
| `dotnet run -e ASPNETCORE_ENVIRONMENT=Production` | Set an environment variable for this run. Here it runs the app as Production. |
| `dotnet watch` | Run and apply code changes with hot reload. Press `Ctrl+R` to force a restart. |
| `dotnet build` | Compile without running. |
| `dotnet run hello.cs` | Run a single C# file with no project file. |

`-e` on `dotnet run` sets an environment variable, not an environment name. Write `-e NAME=VALUE`, and repeat `-e` for more variables. The `--environment` long form does the same thing.

## Packages

| Command | What it does |
| --- | --- |
| `dotnet add package Microsoft.EntityFrameworkCore.Sqlite` | Install the newest stable version of a package. |
| `dotnet add package Humanizer --version 2.14.1` | Install a specific version. |
| `dotnet remove package Humanizer` | Uninstall a package. |
| `dotnet list package` | List installed packages. |
| `dotnet list package --outdated` | Show packages with newer versions. |
| `dotnet list package --vulnerable` | Show packages with known security issues. |
| `dotnet restore` | Download packages without building. |

SDK 10 also accepts the noun-first forms `dotnet package add`, `dotnet package remove`, and `dotnet package list`. They do the same thing.

## EF Core

| Command | What it does |
| --- | --- |
| `dotnet tool install -g dotnet-ef` | Install the EF Core command-line tool once per machine. |
| `dotnet tool update -g dotnet-ef` | Update it to the newest version. |
| `dotnet ef migrations add AddProjects` | Create a migration from your model changes. |
| `dotnet ef migrations list` | List migrations and whether each one is applied. |
| `dotnet ef migrations remove` | Delete the last migration if it is not applied yet. |
| `dotnet ef database update` | Apply every pending migration. |
| `dotnet ef database update InitialCreate` | Move the database to a named migration, which can mean rolling back. |
| `dotnet ef database update 0` | Roll back every migration. |
| `dotnet ef migrations script --idempotent -o migrate.sql` | Write a SQL script that is safe to run on any database version. |
| `dotnet ef migrations bundle` | Build an `efbundle` executable that applies migrations on a server. |
| `dotnet ef database drop` | Delete the database. Asks first. |

The project needs the `Microsoft.EntityFrameworkCore.Design` package for any `dotnet ef` command. See [Migrations](/web-api/migrations).

## Secrets

| Command | What it does |
| --- | --- |
| `dotnet user-secrets init` | Add a `UserSecretsId` to the project file. |
| `dotnet user-secrets set "Jwt:Key" "a-long-random-development-key"` | Store a secret. Read it as `builder.Configuration["Jwt:Key"]`. |
| `dotnet user-secrets list` | Show all secrets for this project. |
| `dotnet user-secrets remove "Jwt:Key"` | Delete one secret. |
| `dotnet user-secrets clear` | Delete all of them. |

User secrets load only in the `Development` environment. See [Configuration](/web-api/configuration).

## Testing

| Command | What it does |
| --- | --- |
| `dotnet test` | Build and run every test project in the folder or solution. |
| `dotnet test --filter "FullyQualifiedName~Todos"` | Run tests whose full name contains the text. |
| `dotnet test --filter "Category=Integration"` | Run tests with a trait or category. |
| `dotnet test --logger "console;verbosity=detailed"` | Print each test name and its output. |

See [Testing](/web-api/testing).

## Publishing

| Command | What it does |
| --- | --- |
| `dotnet publish -c Release -o ./publish` | Build a release copy ready to deploy into `./publish`. |
| `dotnet publish -c Release -r linux-x64 --self-contained` | Include the .NET runtime so the server does not need it. |
| `dotnet publish /t:PublishContainer` | Build a container image without a Dockerfile. Needs Docker or Podman running. |

See [Deployment](/web-api/deployment).

## Certificates

| Command | What it does |
| --- | --- |
| `dotnet dev-certs https --trust` | Create and trust the HTTPS development certificate. |
| `dotnet dev-certs https --check --trust` | Check that a trusted certificate exists. |
| `dotnet dev-certs https --clean` | Remove all development certificates. Run `--trust` after it to get a fresh one. |

## Tools

| Command | What it does |
| --- | --- |
| `dotnet tool install -g dotnet-ef` | Install a global tool for your user account. |
| `dotnet tool list -g` | List global tools. |
| `dotnet new tool-manifest` | Create `.config/dotnet-tools.json` so the repo can pin its own tool versions. |
| `dotnet tool install dotnet-ef` | Without `-g`, install a local tool into that manifest. |
| `dotnet tool restore` | Install every tool in the manifest, for example on a new machine or in CI. |

## Learn more

- [Troubleshooting](/web-api/troubleshooting) when a command fails.
- [.NET CLI overview](https://learn.microsoft.com/dotnet/core/tools/) on Microsoft Learn.
