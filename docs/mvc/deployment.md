# Deployment

## Introduction

Deployment means getting your app off your machine and onto a server where users can reach it. ASP.NET Core apps compile to files that run anywhere .NET is installed, or inside a container. This page covers publishing, configuration, Docker, and the database.

## Publishing the app

```bash
dotnet publish -c Release -o ./publish
```

`dotnet publish` compiles your app and copies everything it needs to run into one folder. `-c Release` builds with optimizations turned on, and `-o ./publish` sets the output folder. Copy that folder to your server.

## Running the published app

```bash
cd publish
dotnet BookShelf.dll
```

A `.dll` file is a compiled .NET program. The `dotnet` command starts it, and the app listens on `http://localhost:5000` by default. The server needs the ASP.NET Core Runtime installed, which is smaller than the SDK you develop with.

To choose a different address, set the `ASPNETCORE_URLS` environment variable, for example `http://0.0.0.0:8080`.

## Environment variables

An environment variable is a named value that the operating system passes to a program when it starts. ASP.NET Core reads them on top of `appsettings.json`, so you can change settings per server without editing files.

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__Default="Data Source=/var/data/bookshelf.db"
dotnet BookShelf.dll
```

`ASPNETCORE_ENVIRONMENT` sets the environment name. A published app already runs as Production when this is not set, which turns off the developer exception page and loads `appsettings.Production.json` if it exists.

The double underscore `__` stands for a section break, so `ConnectionStrings__Default` overrides `ConnectionStrings:Default` from `appsettings.json`. Use this for passwords and other secrets, and keep them out of source control. See [Configuration](/mvc/configuration) for the full order in which settings load.

::: warning
The `export` command works in bash on Linux and macOS. In PowerShell on Windows, use `$env:ASPNETCORE_ENVIRONMENT = "Production"` instead.
:::

## Running in Docker

Docker packages your app and the .NET runtime into an image, a single file that runs the same way on any machine with Docker.

```dockerfile [Dockerfile]
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BookShelf.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BookShelf.dll"]
```

This is a multi-stage build. The first stage uses the SDK image to compile the app. The second stage starts from the smaller ASP.NET Core runtime image and copies in only the published files, so the final image does not contain the SDK or your source code.

```bash
docker build -t bookshelf .
docker run -p 8080:8080 -e ConnectionStrings__Default="Data Source=/data/bookshelf.db" -v bookshelf-data:/data bookshelf
```

The .NET container images listen on port 8080. `-p 8080:8080` maps that port to your machine, so the app is at `http://localhost:8080`. `-v` mounts a volume, a folder that Docker keeps between runs, so the SQLite file survives when the container restarts.

Add a `.dockerignore` file that lists `bin/`, `obj/`, and `publish/`, so local build output does not get copied into the image.

## Running migrations on deploy

The production database needs the same schema as your code. One safe option is a migration bundle, a single program that applies your [migrations](/mvc/migrations).

```bash
dotnet ef migrations bundle -o efbundle
./efbundle --connection "Data Source=/var/data/bookshelf.db"
```

Build the bundle on your machine or build server, copy it to the server, and run it before you start the new version of the app. On Windows, use `-o efbundle.exe` and run `.\efbundle.exe`, because the tool uses the name you give it as is.

::: tip
For a small single-server app, you can apply migrations at startup with `db.Database.Migrate()` inside a scope after `builder.Build()`. Avoid this when several copies of the app start at once, because they will all try to migrate.
:::

## Hosting options

- IIS on Windows Server. Install the ASP.NET Core Hosting Bundle, then point an IIS site at the publish folder.
- Azure App Service. A managed host from Microsoft. Deploy from Visual Studio, the Azure CLI, or GitHub Actions.
- Linux with a reverse proxy. Run the app as a systemd service and put Nginx or Apache in front. A reverse proxy is a web server that receives public traffic and forwards it to your app.
- Any container host that runs Docker images, using the Dockerfile above.

With a reverse proxy, the proxy handles HTTPS. Your app sees plain HTTP, so read the Microsoft guide on forwarded headers before you go live.

## Learn more

- [Host and deploy ASP.NET Core](https://learn.microsoft.com/aspnet/core/host-and-deploy/)
- [Host ASP.NET Core on Linux with Nginx](https://learn.microsoft.com/aspnet/core/host-and-deploy/linux-nginx)
