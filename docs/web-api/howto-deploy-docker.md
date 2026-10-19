# Deploy with Docker

## Introduction

At the end of this guide one `docker compose up` starts `TodoApi`, PostgreSQL, and a Caddy reverse proxy that serves the API on port 80. The app waits for a healthy database and reports its own health at `/health`.

Before you start, you need `TodoApi` on PostgreSQL from [Switch to PostgreSQL](/web-api/howto-postgres), Docker with the compose plugin, and the `/health` endpoint from [Health checks](/web-api/health-checks). The Dockerfile comes from [Deployment](/web-api/deployment).

## 1. Write the Dockerfile

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
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TodoApi.dll"]
```

This is the file from [Deployment](/web-api/deployment) without the SQLite lines. The data now lives in PostgreSQL, so the app container needs no volume.

```text [.dockerignore]
bin/
obj/
```

## 2. Write the compose file

```yaml [compose.yaml]
services:
  db:
    image: postgres:17
    environment:
      POSTGRES_USER: todos
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: todos
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U todos -d todos"]
      interval: 5s
      retries: 10

  api:
    build: .
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_FORWARDEDHEADERS_ENABLED: "true"
      ConnectionStrings__Default: Host=db;Database=todos;Username=todos;Password=${DB_PASSWORD}
    depends_on:
      db:
        condition: service_healthy

  proxy:
    image: caddy:2
    ports:
      - "80:80"
    command: caddy reverse-proxy --from :80 --to api:8080
    depends_on:
      - api

volumes:
  pgdata:
```

Compose starts three containers on a private network where each one reaches the others by service name, so the connection string uses `Host=db`. Only the proxy publishes a port. The API and database are not reachable from outside.

`depends_on` with `service_healthy` holds the API back until `pg_isready` says PostgreSQL accepts connections. `ASPNETCORE_FORWARDEDHEADERS_ENABLED` tells ASP.NET Core to trust the `X-Forwarded-For` and `X-Forwarded-Proto` headers Caddy adds, so logs and redirects see the real client and scheme.

## 3. Keep the password out of the file

```text [.env]
DB_PASSWORD=choose-a-long-password
```

Compose reads `.env` next to `compose.yaml` and fills in `${DB_PASSWORD}`. Add `.env` to `.gitignore`. Pass other secrets, such as `Email__Password`, the same way.

## 4. Run the migrations

```bash
docker compose up -d db
dotnet ef database update --connection "Host=localhost;Port=5432;Database=todos;Username=todos;Password=choose-a-long-password"
```

Before the first command, add `ports: ["5432:5432"]` under `db` so your machine can reach the database, and remove it after migrating. On a server, run a migration bundle instead, as on [Deployment](/web-api/deployment).

## 5. Start everything

```bash
docker compose up -d --build
docker compose ps
docker compose logs -f api
```

`--build` rebuilds the API image from your current code. `ps` shows `db` as `healthy` and the other two as running. `logs -f` follows the API's console output.

## 6. Try it

```bash
curl -i http://localhost/health
```

```http
HTTP/1.1 200 OK
Content-Type: text/plain
Server: Caddy

Healthy
```

The `Server: Caddy` header shows the request went through the proxy. Stop the database with `docker compose stop db` and call `/health` again. With `AddDbContextCheck<AppDbContext>()` from [Health checks](/web-api/health-checks) you get `503` and `Unhealthy`.

To serve HTTPS on a real server, point a domain at it, change the Caddy command to `--from api.example.com`, and publish port `443` too. Caddy gets a certificate for you.

::: warning
The `aspnet` base image has no `curl`, so this compose file does not add a Docker `healthcheck` to `api`. Container platforms such as Kubernetes or Azure Container Apps call `/health/live` and `/health/ready` over HTTP themselves.
:::

## Learn more

- [Deployment](/web-api/deployment)
- [Health checks](/web-api/health-checks)
- [Switch to PostgreSQL](/web-api/howto-postgres)
- [Host ASP.NET Core in Docker containers](https://learn.microsoft.com/aspnet/core/host-and-deploy/docker/)
