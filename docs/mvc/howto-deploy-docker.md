# Deploy with Docker

## Introduction

At the end of this guide, BookShelf and a PostgreSQL database run together with one `docker compose up` command, the data survives restarts, and Caddy serves the site over HTTPS. Before you start, you need Docker with Compose, BookShelf on PostgreSQL from [Switch to PostgreSQL](/mvc/howto-postgres), and the Dockerfile basics from [Deployment](/mvc/deployment).

## 1. Add the Dockerfile

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

This is the multi-stage file from [Deployment](/mvc/deployment). Put it in the `BookShelf` folder, next to `BookShelf.csproj` and `Program.cs`. Copying the project file and restoring first lets Docker cache the packages, so a code change does not download them again.

```text [.dockerignore]
bin/
obj/
publish/
```

## 2. Write the compose file

```yaml [compose.yaml]
services:
  db:
    image: postgres:17
    environment:
      POSTGRES_USER: bookshelf
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: bookshelf
    volumes:
      - db-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U bookshelf"]
      interval: 5s
      retries: 10

  app:
    build: .
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=db;Database=bookshelf;Username=bookshelf;Password=${DB_PASSWORD}"
    depends_on:
      db:
        condition: service_healthy
    volumes:
      - keys:/home/app/.aspnet/DataProtection-Keys

  caddy:
    image: caddy:2
    command: caddy reverse-proxy --from bookshelf.example.com --to app:8080
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - caddy-data:/data
    depends_on:
      - app

volumes:
  db-data:
  keys:
  caddy-data:
```

```text [.env]
DB_PASSWORD=change-me
```

Compose puts the three services on one network, where each one reaches the others by service name. That is why the connection string says `Host=db`. Only Caddy publishes ports, so the app and the database are not reachable from outside.

Compose reads `.env` next to the compose file and fills in `${DB_PASSWORD}`. Add `.env` to `.gitignore`.

The three named volumes keep data when containers are recreated. `db-data` holds the database. `keys` holds the Data Protection keys that encrypt login cookies, so users stay signed in after a redeploy. The .NET images run as a non-root user named `app`, whose home folder is `/home/app`. `caddy-data` holds the HTTPS certificates.

## 3. Let Caddy handle HTTPS

The `caddy reverse-proxy` command is the whole Caddy configuration. Caddy gets a free certificate from Let's Encrypt for `bookshelf.example.com` and forwards requests to the app on port 8080, the port .NET images listen on. Replace the domain with yours, and point its DNS record at the server first. For a local test, use `--from localhost`, and Caddy uses its own local certificate.

The app now sees plain HTTP from Caddy. Tell it the original request was HTTPS, so redirects and cookie settings are right.

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.HttpOverrides;

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
```

`UseForwardedHeaders` must come first in the pipeline. Clearing the known networks trusts the proxy on the Docker network. This is safe only because the app's port is not published.

## 4. Add a health check

```csharp [Program.cs (excerpt)]
builder.Services.AddHealthChecks();

app.MapHealthChecks("/health");
```

`/health` returns `Healthy` with status 200 while the app runs. [Health checks](/mvc/health-checks) shows how to include the database. The .NET runtime image has no `curl`, so check it from outside with `curl https://bookshelf.example.com/health`, or point your host's monitor at it.

## 5. Build, migrate, and start

```bash
docker compose up -d --build
dotnet ef migrations bundle -o efbundle --self-contained -r linux-x64
docker run --rm --network bookshelf_default -v "$(pwd)/efbundle:/efbundle" \
  mcr.microsoft.com/dotnet/runtime-deps:10.0 \
  /efbundle --connection "Host=db;Database=bookshelf;Username=bookshelf;Password=change-me"
```

The first command builds the image and starts all three containers in the background. The second builds a [migration bundle](/mvc/deployment) for Linux that needs no .NET install. The third runs it in a throwaway container on the compose network, where the name `db` resolves. Compose names the network after the folder, so `bookshelf_default` here. Run `docker network ls` if yours differs.

## 6. Try it

```bash
docker compose ps
curl https://bookshelf.example.com/health
```

`docker compose ps` lists `db`, `app`, and `caddy` as running, and `db` as healthy. The curl call returns `Healthy`. Open `https://bookshelf.example.com/Books` in a browser and add a book.

Run `docker compose down` and then `docker compose up -d`. The book is still there, because it lives in the `db-data` volume. `docker compose down -v` would delete the volumes and the data with them.

To see the app's logs, run `docker compose logs -f app`.

## Learn more

- [Deployment](/mvc/deployment)
- [Switch to PostgreSQL](/mvc/howto-postgres)
- [Health checks](/mvc/health-checks)
- [Host ASP.NET Core in Docker containers](https://learn.microsoft.com/aspnet/core/host-and-deploy/docker/)
