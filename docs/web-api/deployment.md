# Deployment

## Introduction

Deployment means building your API in release mode and running it on a server where clients can reach it. This page covers publishing, environment settings, Docker, and keeping the database schema up to date.

## Publishing the app

```bash
dotnet publish -c Release -o ./publish
```

`dotnet publish` compiles the app with optimizations and copies everything it needs into one folder. `-c Release` picks the Release configuration, which is faster than the default Debug build. `-o ./publish` sets the output folder.

```bash
cd publish
dotnet TodoApi.dll
```

The server needs the ASP.NET Core 10 runtime installed to run the app this way. The app listens on port 5000 by default unless you set a URL.

## Setting environment variables

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__Default="Data Source=/var/data/todos.db"
export ASPNETCORE_URLS="http://0.0.0.0:8080"
```

Environment variables override values from `appsettings.json`, so you change settings per server without editing files. `ASPNETCORE_ENVIRONMENT` picks the environment, and it defaults to `Production` when not set. A double underscore stands for the `:` separator, so `ConnectionStrings__Default` sets `ConnectionStrings:Default`. See [Configuration](/web-api/configuration).

::: warning
Do not put production secrets such as signing keys or database passwords in `appsettings.json`. Use environment variables or a secret store such as Azure Key Vault.
:::

## Running migrations on deploy

Add the migration lines to your existing file. The block below shows where they go, not a file to paste over yours.

```csharp [Program.cs (excerpt)]
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapTodoEndpoints();
app.Run();
```

`Migrate()` applies any [migrations](/web-api/migrations) the database has not seen yet, then the app starts. This is fine for a small app with one instance and SQLite. If several copies of your app start at once, they can race to migrate the same database.

```bash
dotnet ef migrations bundle -o efbundle
./efbundle --connection "Data Source=/var/data/todos.db"
```

For larger setups, build a migration bundle instead. A bundle is a single executable that applies your migrations, and you run it as a separate step in your release pipeline before the new app version starts.

## Building a Docker image

```dockerfile [Dockerfile]
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TodoApi.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir /data && chown $APP_UID /data
USER $APP_UID
ENV ConnectionStrings__Default="Data Source=/data/todos.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "TodoApi.dll"]
```

Docker packages your app and its runtime into an image that runs the same way on any machine. This file is a multi-stage build. The first stage uses the large SDK image to compile, and the second copies only the published output into the smaller `aspnet` runtime image.

Copying the `.csproj` and restoring before copying the rest lets Docker cache the restore step, so rebuilds are faster when only your code changes. `USER $APP_UID` runs the app as a non-root user that the base image provides. The .NET images listen on port 8080 by default.

```bash
docker build -t todoapi .
docker run -p 8080:8080 -v todo-data:/data todoapi
```

The `-v` flag mounts a volume, a storage area that survives when the container is removed, so your SQLite file is not lost. Add a `.dockerignore` file that lists `bin/` and `obj/` to keep build output out of the image.

## Publishing a container without a Dockerfile

```bash
dotnet publish /t:PublishContainer
```

The .NET SDK can build a container image directly, with no Dockerfile. It picks the right base image for you and names the image after your project, here `todoapi`. You still need Docker or Podman running locally to store the image.

## Hosting options

- Azure App Service. Managed hosting where you deploy the publish folder or a container.
- Azure Container Apps, AWS ECS, or Google Cloud Run. Run your Docker image without managing servers.
- A Linux virtual machine. Run the app behind Nginx or Caddy as a reverse proxy, a server that receives public traffic and forwards it to your app.
- IIS on Windows Server. Install the ASP.NET Core Hosting Bundle first.

SQLite works well for small apps on a single server. If you run several instances, move to a server database such as PostgreSQL or SQL Server, which only needs a different EF Core provider package and connection string.

## Learn more

- [Host and deploy ASP.NET Core](https://learn.microsoft.com/aspnet/core/host-and-deploy/)
- [Containerize a .NET app with dotnet publish](https://learn.microsoft.com/dotnet/core/containers/sdk-publish)
