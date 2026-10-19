# Switch to PostgreSQL

## Introduction

At the end of this guide, BookShelf stores its data in PostgreSQL instead of a SQLite file, with a fresh set of migrations made for PostgreSQL. Before you start, you need BookShelf with `AppDbContext` from [Database](/mvc/database), the `dotnet ef` tool from [Migrations](/mvc/migrations), and Docker installed to run the database locally.

## 1. Start PostgreSQL in Docker

```bash
docker run -d --name bookshelf-db \
  -e POSTGRES_USER=bookshelf \
  -e POSTGRES_PASSWORD=devpassword \
  -e POSTGRES_DB=bookshelf \
  -p 5432:5432 \
  -v bookshelf-pg:/var/lib/postgresql/data \
  postgres:17
```

This runs PostgreSQL 17 in the background and creates the `bookshelf` user and database on first start. `-v bookshelf-pg:...` stores the data in a Docker volume, so it survives `docker stop` and `docker rm`. On PowerShell, put the command on one line or replace each `\` with a backtick.

## 2. Swap the provider package

```bash
dotnet remove package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

Npgsql is the EF Core provider for PostgreSQL. It is maintained by the Npgsql team and is the one Microsoft's docs point to.

## 3. Change the connection string

```json [appsettings.json (excerpt)]
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=bookshelf;Username=bookshelf"
}
```

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=bookshelf;Username=bookshelf;Password=devpassword"
```

A PostgreSQL connection string uses `Host`, `Database`, `Username`, and `Password` keys instead of SQLite's `Data Source`. User secrets override the value in `appsettings.json` in Development, so the password stays out of source control. In production, set `ConnectionStrings__Default` as an environment variable.

## 4. Call UseNpgsql

```csharp [Program.cs (excerpt)]
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

This is the only code change. Controllers and queries stay the same, because they talk to `AppDbContext`, not to the database.

## 5. Replace the migrations

```bash
rm -r Migrations
dotnet ef migrations add InitialCreate
dotnet ef database update
```

On Windows PowerShell, use `Remove-Item -Recurse Migrations`. If your migrations live in `Data/Migrations`, delete that folder instead.

Migrations are provider-specific. The SQLite migrations contain SQLite column types such as `TEXT` and `INTEGER`, and SQLite settings such as `Sqlite:Autoincrement`. PostgreSQL needs its own types such as `text`, `integer`, and identity columns. Applying the old files to PostgreSQL either fails or creates the wrong column types. Deleting them and creating one new `InitialCreate` gives you a clean history for the new database.

The model snapshot file is in the same folder and goes with them. Your `HasData` seed data is in the model, so the new migration includes it.

::: warning
Do this before you have a production PostgreSQL database. After that, add normal migrations on top and never delete them.
:::

## 6. Copy your existing data

The new database starts with the schema and seed data only. If your SQLite file has rows you want to keep, copy them across once.

- For a small app, export each table to CSV (see [Export data to CSV](/mvc/howto-csv-export)) and load it with PostgreSQL's `\copy` command in `psql`.
- For more tables, use `pgloader`, a free tool that reads a SQLite file and writes it into PostgreSQL in one command.

After copying rows with explicit ids, reset each identity sequence, for example `SELECT setval(pg_get_serial_sequence('"Books"', 'Id'), MAX("Id")) FROM "Books";`. Otherwise the next insert fails with a duplicate key error.

PostgreSQL compares strings case-sensitively. A search such as `b.Title.Contains(search)` that matched any case in SQLite now only matches the exact case. Use `EF.Functions.ILike(b.Title, $"%{search}%")` where you need case-insensitive search.

## 7. Try it

Run `dotnet run` and open `https://localhost:<port>/Books`. Add a book. Then look at the table directly.

```bash
docker exec -it bookshelf-db psql -U bookshelf -c 'SELECT "Id", "Title" FROM "Books";'
```

You see your new book in the result. EF Core quotes table and column names in PostgreSQL, so you must quote them too, with the same capital letters.

## Learn more

- [Database](/mvc/database)
- [Migrations](/mvc/migrations)
- [Deploy with Docker](/mvc/howto-deploy-docker)
- [Database providers in EF Core](https://learn.microsoft.com/ef/core/providers/)
