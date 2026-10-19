# Queues and retries

## Introduction

Some work should not block the response: sending a reminder email, generating a report, calling a slow outside service. The [Background tasks](/web-api/background-tasks) page uses an in-memory queue for this. This page adds a persistent job queue that keeps jobs across restarts and retries them when they fail. You need it as soon as losing a job matters. For work you can afford to drop, the in-memory queue is fine.

<ApiStyle />

## Why an in-memory queue loses work

A `Channel<T>` with a `BackgroundService` reader lives in the app's memory. Three things lose jobs:

1. The app restarts during a deploy, and everything still in the channel is gone.
2. The job throws, and nothing tries it again unless you write that loop yourself.
3. You run two instances, and each has its own queue with no shared view of what is pending.

A job queue fixes all three by storing each job in a database before running it.

## Installing Hangfire

```bash
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.InMemory
```

Hangfire is the most used job library for .NET. It stores jobs in a database, runs them on worker threads inside your app, retries failures, and comes with a dashboard. `Hangfire.InMemory` is a storage for local development so you can start without setting up a database.

```csharp [Program.cs (excerpt)]
using Hangfire;

builder.Services.AddHangfire(config => config.UseInMemoryStorage());
builder.Services.AddHangfireServer();
```

`AddHangfire` registers the client you use to create jobs. `AddHangfireServer` starts the workers that run them.

::: warning
In-memory storage loses jobs on restart, which is the problem you came here to solve. In production, use real storage such as `Hangfire.SqlServer` with `config.UseSqlServerStorage(connectionString)`, or a community package for PostgreSQL. Hangfire creates its own tables on first run.
:::

## Writing a job

```csharp [Jobs/TodoReminderJob.cs]
using Hangfire;
using TodoApi.Data;

namespace TodoApi.Jobs;

public class TodoReminderJob(AppDbContext db, ILogger<TodoReminderJob> logger)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task SendAsync(int todoId)
    {
        var todo = await db.Todos.FindAsync(todoId);
        if (todo is null || todo.IsDone)
        {
            return;
        }

        logger.LogInformation("Reminder for todo {Id}: {Title}", todo.Id, todo.Title);
    }
}
```

A job is a public method on a normal class. Hangfire creates the class through DI in its own scope for each run, so it can ask for `AppDbContext` like any service. Replace the log line with your [email](/web-api/email) sender.

Hangfire stores the method name and its arguments, not the object, so pass small values such as an id. Load the data inside the job, because it may run minutes later.

## Enqueueing from an endpoint

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
using Hangfire;
using TodoApi.Jobs;

group.MapPost("/{id:int}/remind", Remind);

static Accepted Remind(int id, IBackgroundJobClient jobs)
{
    jobs.Enqueue<TodoReminderJob>(job => job.SendAsync(id));
    return TypedResults.Accepted($"/todos/{id}");
}
```

This replaces the `Remind` handler and its `MapPost` line from [Background tasks](/web-api/background-tasks), so delete those first. Two endpoints on the same route make every request to it fail.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using Hangfire;
using TodoApi.Jobs;

public class TodosController(AppDbContext db, IBackgroundJobClient jobs) : ControllerBase
{
    [HttpPost("{id:int}/remind")]
    public IActionResult Remind(int id)
    {
        jobs.Enqueue<TodoReminderJob>(job => job.SendAsync(id));
        return Accepted($"/todos/{id}");
    }
}
```

This replaces the `Remind` action from [Background tasks](/web-api/background-tasks), so delete it first, along with any constructor parameter only it used. Two actions on the same route make every request to it fail.

</div>

`Enqueue` writes the job to storage and returns at once. A worker picks it up within seconds. The endpoint returns `202 Accepted`, which means "received, will be processed later."

## Retries

When a job throws, Hangfire schedules it again with a growing delay between attempts. The default is 10 attempts. `[AutomaticRetry(Attempts = 5)]` on the method changes that for one job, and `Attempts = 0` turns retries off.

Because a job can run more than once, write it so a second run does no harm. The reminder job checks `IsDone` first. For an email, record that it was sent and skip it if the record exists.

## Recurring jobs

```csharp [Jobs/PurgeDoneTodosJob.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Jobs;

public class PurgeDoneTodosJob(AppDbContext db)
{
    public Task RunAsync() =>
        db.Todos.Where(t => t.IsDone).ExecuteDeleteAsync();
}
```

```csharp [Program.cs (excerpt)]
using Hangfire;
using TodoApi.Jobs;

var app = builder.Build();

var recurring = app.Services.GetRequiredService<IRecurringJobManager>();
recurring.AddOrUpdate<PurgeDoneTodosJob>(
    "cleanup-done-todos",
    job => job.RunAsync(),
    Cron.Daily());
```

The first argument is a stable id. Calling `AddOrUpdate` on every startup updates the schedule instead of adding a duplicate. `Cron.Daily()` runs at midnight UTC. Pass any cron expression for other schedules, such as `"0 */6 * * *"` for every six hours. The static `RecurringJob.AddOrUpdate` does the same thing once Hangfire has started.

The class has a different name from the `CleanupJob` hosted service on [Background tasks](/web-api/background-tasks), so both can live in the project. If you switch to this one, remove `AddHostedService<CleanupJob>()`.

## The dashboard

```csharp [Program.cs (excerpt)]
app.UseAuthentication();
app.UseAuthorization();

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = []
}).RequireAuthorization("admin");
```

The dashboard at `/hangfire` shows queued, running, failed and recurring jobs, and lets you retry or delete them. By default it only allows requests from the local machine. Setting `Authorization = []` removes that check so `RequireAuthorization("admin")` can guard it instead, using the policy from [Authorization](/web-api/authorization). Never expose it without authorization, because anyone who opens it can delete or trigger jobs.

The dashboard uses cookies and a browser, so a JWT-only API needs a cookie login for admins or a dashboard that is reachable only from an internal network.

## The outbox alternative

If you do not want a job library, use an outbox table. In the same `SaveChangesAsync` that creates the todo, also insert a row into an `OutboxMessages` table describing the work. Both rows commit in one transaction, so the work is never lost or recorded without the todo. A `BackgroundService` polls the table, does the work, and marks each row done or increments a retry count. This is more code to own, but it needs nothing beyond EF Core and is the standard way to publish events to a message broker reliably.

## Learn more

- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
