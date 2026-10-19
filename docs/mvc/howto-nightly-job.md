# Run a job every night

## Introduction

At the end of this guide, BookShelf runs a job every night at 02:00 server time that reads the database and logs a report. You can swap the report for any work, such as cleaning old data. Before you start, you need `AppDbContext` from [Database](/mvc/database). [Background tasks](/mvc/background-tasks) explains hosted services in more depth.

## 1. Write the service

```csharp [Services/NightlyReportService.cs]
using BookShelf.Data;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public class NightlyReportService(
    IServiceScopeFactory scopeFactory,
    ILogger<NightlyReportService> logger) : BackgroundService
{
    private static readonly TimeSpan RunAt = new(2, 0, 0); // 02:00 local time

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRun(DateTime.Now);
            logger.LogInformation("Next nightly run in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);

            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Nightly job failed");
            }
        }
    }

    private static TimeSpan DelayUntilNextRun(DateTime now)
    {
        var next = now.Date + RunAt;
        if (next <= now)
        {
            next = next.AddDays(1);
        }
        return next - now;
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var count = await db.Books.CountAsync(ct);
        logger.LogInformation("Nightly report: {Count} books on the shelf", count);
    }
}
```

`BackgroundService` runs `ExecuteAsync` once when the app starts, and the loop keeps it alive. Each pass works out how long to wait until 02:00 and sleeps until then. If it is already past 02:00 today, the next run is tomorrow.

The service computes the delay again after each run instead of waiting a fixed 24 hours. The run itself takes time, and fixed waits would drift later every day.

## 2. Get the DbContext from a scope

The hosted service is a singleton, created once for the whole app. `AppDbContext` is scoped, created once per request. A singleton cannot take a scoped service in its constructor. ASP.NET Core throws "Cannot consume scoped service" at startup in Development if you try.

`IServiceScopeFactory` solves this. `CreateScope` gives you a fresh scope for each run, the `using` disposes it, and the context closes with it. Put all your database work inside `RunOnceAsync`.

## 3. Keep errors from stopping the loop

The `try`/`catch` logs a failed run and lets the loop wait for the next night. Without it, one exception ends `ExecuteAsync`, and since .NET 6 an unhandled exception in a `BackgroundService` stops the whole app.

The `when` filter lets `OperationCanceledException` through. That exception means the app is shutting down, and the loop should end.

## 4. Register it

```csharp [Program.cs (excerpt)]
using BookShelf.Services;

builder.Services.AddHostedService<NightlyReportService>();
```

`AddHostedService` starts the service with the app and stops it on shutdown.

## 5. Know the limits

`DateTime.Now` uses the server's time zone. Many cloud hosts and containers run in UTC, so 02:00 there may be the middle of your working day. Set `RunAt` in UTC, or set the `TZ` environment variable on the server.

This service lives inside the web app, which brings three limits:

- If the app is stopped or restarting at 02:00, that night's run is skipped.
- If you run two copies of the app, the job runs twice.
- On hosts that unload idle apps, such as IIS or App Service without Always On, nothing runs while the app is asleep.

When any of these matter, use Hangfire with a recurring job, as shown in [Queues and retries](/mvc/queues). Hangfire stores the schedule in a database, runs each job once across all servers, and retries failures.

## 6. Try it

To see a run without waiting until 2 AM, change `RunAt` to two minutes from now, for example `new(14, 32, 0)`. Run `dotnet run` and watch the console.

```text
info: BookShelf.Services.NightlyReportService[0]
      Next nightly run in 00:01:58.4410000
info: BookShelf.Services.NightlyReportService[0]
      Nightly report: 3 books on the shelf
info: BookShelf.Services.NightlyReportService[0]
      Next nightly run in 23:59:59.9120000
```

The third line shows the loop scheduled the next run for the same time tomorrow. Set `RunAt` back to `new(2, 0, 0)` when you are done.

## Learn more

- [Background tasks](/mvc/background-tasks)
- [Queues and retries](/mvc/queues)
- [Dependency injection](/mvc/dependency-injection)
- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
