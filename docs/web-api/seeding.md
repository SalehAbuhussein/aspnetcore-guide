# Seeding

## Introduction

Seeding means filling the database with starting data. This page shows the two ways EF Core does it: `HasData` for fixed rows that every database needs, and `UseSeeding` for sample data you only want while developing.

## Pick the right tool

| You need | Use |
|---|---|
| Fixed rows every environment needs, such as a default project | `HasData` |
| Sample todos to click around with in development | `UseSeeding` and `UseAsyncSeeding` |
| Large or random test data | `UseSeeding` with a loop, or a library such as Bogus |

`HasData` writes the rows into a migration. `UseSeeding` runs C# code each time the database is created or migrated. Use `UseSeeding` unless the data truly belongs in every database.

## Fixed data with HasData

```csharp [Data/AppDbContext.cs (excerpt)]
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Project>().HasData(
        new Project { Id = 1, Name = "Inbox" });
}
```

Replace the body of `OnModelCreating` with this. It removes the two `Todo` rows that [Migrations](/web-api/migrations) seeded with `HasData`, so the `SeedInbox` migration below deletes them from the database. The sample todos come back from `UseSeeding` further down, in Development only. The `DbSet` properties stay as they are.

```bash
dotnet ef migrations add SeedInbox
dotnet ef database update
```

`HasData` stores the rows in the model, so the next migration contains an `InsertData` call. You must set `Id` yourself, because EF Core uses it to know whether a row was added, changed, or removed since the last migration. Change the data later and the next migration updates the row.

`HasData` has limits. You cannot set navigation properties such as `Todos`, only foreign keys like `ProjectId`. Values must be constants, so `DateTime.Now` produces a new migration every time. The [Migrations](/web-api/migrations) page has another short example.

## Sample data with UseSeeding

EF Core 9 added `UseSeeding` and `UseAsyncSeeding`. You pass them a function, and EF Core runs it after it creates or migrates the database.

```csharp [Program.cs (excerpt)]
using TodoApi.Models;

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("Default"));

    if (builder.Environment.IsDevelopment())
    {
        options
            .UseSeeding((context, _) =>
            {
                if (context.Set<Todo>().Any())
                {
                    return;
                }

                context.Set<Todo>().AddRange(SampleData.Todos());
                context.SaveChanges();
            })
            .UseAsyncSeeding(async (context, _, cancellationToken) =>
            {
                if (await context.Set<Todo>().AnyAsync(cancellationToken))
                {
                    return;
                }

                context.Set<Todo>().AddRange(SampleData.Todos());
                await context.SaveChangesAsync(cancellationToken);
            });
    }
});
```

```csharp [Data/SampleData.cs]
using TodoApi.Models;

namespace TodoApi.Data;

public static class SampleData
{
    public static List<Todo> Todos() =>
    [
        new() { Title = "Buy milk" },
        new() { Title = "Walk the dog", IsDone = true },
        new() { Title = "Read the EF Core docs", ProjectId = 1 },
        new() { Title = "Write tests", ProjectId = 1 }
    ];
}
```

The `if` around the seeding calls uses `builder.Environment`, so production databases never get sample rows. The `Any` check makes the seeder safe to run many times. Without it you would get a fresh copy of the sample todos on every start.

The context passed in is a plain `DbContext`, so you reach tables with `context.Set<Todo>()`. Leave `Id` unset here. SQLite assigns it, unlike with `HasData`.

::: tip
Provide both `UseSeeding` and `UseAsyncSeeding` with the same logic. `dotnet ef database update` calls the sync version, and your app calls the async one.
:::

## Running the seeder

The seeder runs when you call `dotnet ef database update`, or when the app calls `EnsureCreated` or `Migrate` itself. To migrate and seed on every start in development, add this after `builder.Build()`:

```csharp [Program.cs (excerpt)]
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

`CreateScope` gives you a short-lived scope, because `AppDbContext` is a scoped service and there is no HTTP request yet. See [Dependency injection](/web-api/dependency-injection) for scopes. `MigrateAsync` applies any pending migrations, then runs `UseAsyncSeeding`.

To start over, delete `todos.db` and run the app again. You get a fresh database with the sample data in it.

::: warning
The `IsDevelopment` checks keep sample data out of production. How you migrate a production database is a separate choice, covered on [Deployment](/web-api/deployment).
:::

## Learn more

- [Data seeding](https://learn.microsoft.com/ef/core/modeling/data-seeding)
- [Applying migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying)
