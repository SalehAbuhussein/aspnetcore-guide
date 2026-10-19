# Queues and retries

## Introduction

Some work should not happen inside the request, such as sending an email when a review is posted or rebuilding a report every night. The user should not wait for it, and if it fails it should try again. This page uses Hangfire to run jobs that survive restarts and retry on failure. If your app only has a quick fire-and-forget task and losing one now and then is fine, the in-process queue from [Background tasks](/mvc/background-tasks) is enough.

## Why an in-memory queue loses work

A `Channel` based queue lives in the app's memory. When the app restarts for a deploy, a crash, or a scale-down, every job still waiting in the channel is gone. Nothing records that it existed, and nothing retries it. A durable queue writes each job to storage first, so a fresh process picks up where the old one stopped.

## Installing Hangfire

Hangfire is the most widely used job library for .NET. It stores jobs in a database and runs them in the background of your app.

```bash
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.InMemory
```

```csharp [Program.cs (excerpt)]
using Hangfire;

builder.Services.AddHangfire(config => config.UseInMemoryStorage());
builder.Services.AddHangfireServer();
```

`AddHangfire` sets up the client that saves jobs. `AddHangfireServer` starts workers inside your app that pick jobs up and run them. In-memory storage is for development only, since it loses jobs on restart exactly like a channel.

::: warning
Use real storage in production. For SQL Server, add `Hangfire.SqlServer` and call `config.UseSqlServerStorage(connectionString)`. Community packages exist for PostgreSQL and SQLite. Hangfire creates its own tables on first start.
:::

## Writing a job

A job is a public method on a class that the DI container can build.

```csharp [Jobs/ReviewNotifier.cs]
using BookShelf.Data;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Jobs;

public class ReviewNotifier(AppDbContext db, ILogger<ReviewNotifier> logger)
{
    public async Task NotifyAsync(int reviewId)
    {
        var review = await db.Reviews
            .Include(r => r.Book)
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        if (review is null) return;

        // Send the email here, for example with the sender from the Email page.
        logger.LogInformation("New {Rating} star review for {Title}",
            review.Rating, review.Book?.Title);
    }

    public async Task SendDailyDigestAsync()
    {
        var count = await db.Reviews.CountAsync();
        logger.LogInformation("BookShelf has {Count} reviews", count);
    }
}
```

Pass IDs to jobs, not whole objects. Hangfire saves the arguments as JSON, and the row may change before the job runs. Loading it fresh inside the job always gives current data.

## Enqueueing a job

```csharp [Controllers/BooksController.cs (excerpt)]
using BookShelf.Jobs;
using Hangfire;

public class BooksController(AppDbContext db, IBackgroundJobClient jobs) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(int id, [Bind("Rating,Body")] Review review)
    {
        review.BookId = id;
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        jobs.Enqueue<ReviewNotifier>(n => n.NotifyAsync(review.Id));

        return RedirectToAction(nameof(Details), new { id });
    }
}
```

`Enqueue` writes the job to storage and returns at once. A Hangfire worker then creates a `ReviewNotifier` in its own DI scope and calls `NotifyAsync`. Enqueue after `SaveChangesAsync`, so the review has an ID and exists when the job looks for it.

## Automatic retries

If a job throws, Hangfire retries it later with growing delays. The default is 10 attempts. After that the job moves to the Failed list in the dashboard, where you can retry it by hand.

```csharp [Jobs/ReviewNotifier.cs (excerpt)]
using Hangfire;

[AutomaticRetry(Attempts = 3)]
public async Task NotifyAsync(int reviewId)
```

Because a job can run more than once, write it so a second run does no harm. For an email, record that it was sent and skip it if the record exists.

## Recurring jobs

```csharp [Program.cs (excerpt)]
var app = builder.Build();

var recurring = app.Services.GetRequiredService<IRecurringJobManager>();
recurring.AddOrUpdate<ReviewNotifier>(
    "daily-digest",
    n => n.SendDailyDigestAsync(),
    Cron.Daily(3));
```

`AddOrUpdate` registers a job under an ID, here `daily-digest`, and runs it on a schedule. `Cron.Daily(3)` means every day at 03:00 UTC. You can pass a raw cron string such as `"*/15 * * * *"` for every 15 minutes. Running it again with the same ID updates the schedule instead of adding a second job.

## The dashboard

Hangfire ships with a web dashboard that lists queued, running, failed, and recurring jobs. By default it only answers requests from the local machine. Protect it with a filter that checks your [authorization](/mvc/authorization) roles.

```csharp [Jobs/AdminDashboardFilter.cs]
using Hangfire.Dashboard;

namespace BookShelf.Jobs;

public class AdminDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;
        return user.IsInRole("Admin");
    }
}
```

```csharp [Program.cs (excerpt)]
using BookShelf.Jobs;

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new AdminDashboardFilter()]
});
```

Put the dashboard after the auth middleware so `User` is filled in. Open `/hangfire` while signed in as an admin to see your jobs.

## The outbox alternative

If you do not want another library, you can use an outbox table. Add a table such as `PendingEmails`, insert a row in the same `SaveChangesAsync` call that saves the review, and have a background service poll the table, send each email, and mark it done. Because the row and the review are saved in one transaction, you never lose a job or send one for data that was not saved. You write the retry and cleanup logic yourself, which is why Hangfire is the better default for most apps.

## Learn more

- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
