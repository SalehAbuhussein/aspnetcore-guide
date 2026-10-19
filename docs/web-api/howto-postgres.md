# Switch to PostgreSQL

## Introduction

At the end of this guide `TodoApi` stores its data in PostgreSQL running in Docker instead of a SQLite file, with a fresh set of migrations. Your endpoints and `AppDbContext` do not change.

Before you start, you need `TodoApi` with EF Core and migrations from [Database](/web-api/database) and [Migrations](/web-api/migrations), and Docker installed. PostgreSQL is the usual next step once you run more than one instance of the app, as [Deployment](/web-api/deployment) explains.

## 1. Start PostgreSQL

```bash
docker run -d --name todos-db -p 5432:5432 \
  -e POSTGRES_USER=todos -e POSTGRES_PASSWORD=dev-password -e POSTGRES_DB=todos \
  -v todos-pg:/var/lib/postgresql/data \
  postgres:17
```

This starts PostgreSQL 17 in the background on port `5432`, with a user, password, and empty database named `todos`. The `-v` volume keeps the data when the container is removed. Stop it with `docker stop todos-db` and start it again with `docker start todos-db`.

## 2. Swap the provider package

```bash
dotnet remove package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

Npgsql is the EF Core provider for PostgreSQL. It is maintained by the Npgsql project, not Microsoft, and its major version follows EF Core's, so version 10 goes with EF Core 10.

## 3. Change the registration

```csharp [Program.cs (excerpt)]
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

`UseNpgsql` replaces `UseSqlite`. Nothing else in your code mentions the database type, so this is the only C# change.

## 4. Change the connection string

```json [appsettings.Development.json (excerpt)]
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=todos;Username=todos;Password=dev-password"
  }
}
```

A PostgreSQL connection string names the server, database, and login. The development password can live here because it only opens a container on your machine. Remove the SQLite value from `appsettings.json` too, or set the production string there with no password and supply the password with an environment variable named `ConnectionStrings__Default`, as on [Configuration](/web-api/configuration).

## 5. Start a new set of migrations

```bash
rm -r Migrations
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Your old migrations were written for SQLite. They contain SQLite column types such as `TEXT` and `INTEGER`, so they do not suit PostgreSQL. With no production data to keep, the simplest path is to delete the folder and create one new migration. EF Core now writes PostgreSQL types such as `integer`, `text`, `boolean`, and `timestamp with time zone`.

If you already have SQLite data you must keep, export it first, for example with the CSV export from [Export data to CSV](/web-api/howto-csv-export), and load it into the new tables.

## 6. Check your dates

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
todo.CompletedAt = input.IsDone ? todo.CompletedAt ?? DateTime.UtcNow : null;
```

Npgsql maps `DateTime` to `timestamp with time zone` and only accepts values whose `Kind` is `Utc`. Saving `DateTime.Now` throws an error that says so. The `TodoApi` code already uses `DateTime.UtcNow`, so search for any `DateTime.Now` you added yourself.

## 7. Try it

Run the app with `dotnet run` and create a todo.

```bash
curl -i -X POST http://localhost:5000/todos -H "Content-Type: application/json" -d '{"title":"Stored in Postgres"}'
```

You get `201 Created`. Now look in the database:

```bash
docker exec -it todos-db psql -U todos -d todos -c 'SELECT "Id", "Title", "IsDone" FROM "Todos";'
```

```text
 Id |       Title        | IsDone
----+--------------------+--------
  1 | Stored in Postgres | f
(1 row)
```

EF Core keeps your C# names, so tables and columns use capital letters and need double quotes in raw SQL.

::: tip
For tests that run against a real PostgreSQL instance, see [Testcontainers](/web-api/testcontainers).
:::

## Learn more

- [Database](/web-api/database)
- [Migrations](/web-api/migrations)
- [Deploy with Docker](/web-api/howto-deploy-docker)
- [Npgsql Entity Framework Core provider](https://learn.microsoft.com/ef/core/providers/?tabs=dotnet-core-cli)
