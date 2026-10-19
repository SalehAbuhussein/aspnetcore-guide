# Run a job every night

## Introduction

At the end of this guide `TodoApi` runs a cleanup job at 02:00 every night that deletes todos finished more than 30 days ago. It is a plain `BackgroundService`, with no extra packages.

Before you start, you need the `CompletedAt` column from [Background tasks](/web-api/background-tasks), which also explains hosted services and scopes in more depth.

## 1. Write the job

```csharp [Background/NightlyJob.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Background;

public class NightlyJob(IServiceScopeFactory scopeFactory, ILogger<NightlyJob> logger) : BackgroundService
{
    static readonly TimeOnly RunAt = new(2, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun(DateTime.Now);
            logger.LogInformation("Nightly job sleeping for {Delay}", delay);
            await Task.Delay(delay, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow.AddDays(-30);
                var deleted = await db.Todos
                    .Where(t => t.IsDone && t.CompletedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                logger.LogInformation("Nightly job removed {Count} todos", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Nightly job failed");
            }
        }
    }

    static TimeSpan TimeUntilNextRun(DateTime now)
    {
        var next = now.Date + RunAt.ToTimeSpan();
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }
}
```

`TimeUntilNextRun` finds today's 02:00, or tomorrow's if it has passed, and the loop sleeps until then. Working out the wait each time keeps the job on 02:00, unlike a 24 hour `PeriodicTimer` that would drift to whenever the app last started.

The job is a singleton, but `AppDbContext` is scoped, so it creates its own scope for each run. The `try` block logs failures and waits for the next night instead of stopping the app. Both points are covered on [Background tasks](/web-api/background-tasks).

## 2. Register it

```csharp [Program.cs (excerpt)]
using TodoApi.Background;

builder.Services.AddHostedService<NightlyJob>();
```

The job starts with the app and stops when the app shuts down, which cancels `stoppingToken` and ends `Task.Delay`.

## 3. Mind the clock

`DateTime.Now` uses the server's time zone. Docker images and most cloud hosts run in UTC, so 02:00 there may be the middle of your working day. To pin the job to a zone, convert first:

```csharp [Background/NightlyJob.cs (excerpt)]
var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
var delay = TimeUntilNextRun(TimeZoneInfo.ConvertTime(DateTime.Now, zone));
```

On days when clocks change, the wait can be one hour off. For a cleanup job that does not matter.

## 4. Try it

You do not want to wait until 02:00. Change `RunAt` to two minutes from now, for example `new(14, 32)`, and run the app with `dotnet run`. The console shows:

```text
info: TodoApi.Background.NightlyJob[0]
      Nightly job sleeping for 00:01:58.4410000
info: TodoApi.Background.NightlyJob[0]
      Nightly job removed 0 todos
info: TodoApi.Background.NightlyJob[0]
      Nightly job sleeping for 23:59:59.9800000
```

To see a row go, set one todo's `CompletedAt` to a date more than 30 days ago in the database, then wait for the run. The log shows `removed 1 todos`. Put `RunAt` back to `new(2, 0)` when you are done.

::: warning
This job lives inside the web app. If you run three instances, it runs three times, and if the app is down at 02:00, that night is skipped. When a missed or doubled run matters, use a Hangfire recurring job with `Cron.Daily(2)` from [Queues and retries](/web-api/queues), which stores the schedule in the database and runs it once.
:::

## Learn more

- [Background tasks](/web-api/background-tasks)
- [Queues and retries](/web-api/queues)
- [Dependency injection](/web-api/dependency-injection)
- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
