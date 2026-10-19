# Background tasks

## Introduction

Some work should not happen inside a request. This page adds a job that cleans up old todos every hour, and an in-memory queue so an endpoint can return `202 Accepted` right away while the email goes out in the background.

<ApiStyle />

## Hosted services

A hosted service is a class that ASP.NET Core starts with the app and stops when the app shuts down. The usual way to write one is to inherit from `BackgroundService` and put your loop in `ExecuteAsync`. It runs alongside your endpoints for the whole life of the app.

## A recurring cleanup job

The job deletes todos that have been done for 30 days, so it needs to know when a todo was finished:

```csharp [Models/Todo.cs (excerpt)]
public DateTime? CompletedAt { get; set; }
```

```bash
dotnet ef migrations add AddCompletedAt
dotnet ef database update
```

Set it in `Update` when the todo changes state:

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
todo.Title = input.Title;
todo.IsDone = input.IsDone;
todo.CompletedAt = input.IsDone ? todo.CompletedAt ?? DateTime.UtcNow : null;
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
todo.Title = input.Title;
todo.IsDone = input.IsDone;
todo.CompletedAt = input.IsDone ? todo.CompletedAt ?? DateTime.UtcNow : null;
```

</div>

Now the job:

```csharp [Background/CleanupJob.cs]
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Background;

public class CleanupJob(IServiceScopeFactory scopeFactory, ILogger<CleanupJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow.AddDays(-30);
                var deleted = await db.Todos
                    .Where(t => t.IsDone && t.CompletedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                logger.LogInformation("Cleanup removed {Count} todos", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Cleanup failed");
            }
        }
    }
}
```

`PeriodicTimer` waits for the next tick each time round the loop. When the app shuts down, `stoppingToken` is cancelled and `WaitForNextTickAsync` throws, which ends the loop. `BackgroundService` treats that as a normal stop. `ExecuteDeleteAsync` sends one `DELETE` statement to the database without loading the rows first.

The `try` block matters. Since .NET 6, an unhandled exception in a `BackgroundService` stops the whole app. Catch, log, and wait for the next tick.

## Why you need IServiceScopeFactory

A hosted service is a singleton: one instance for the life of the app. `AppDbContext` is scoped: one instance per request. A singleton must not hold a scoped service, as the [Dependency injection](/web-api/dependency-injection) page warns, and there is no request here to create a scope for you.

`IServiceScopeFactory` lets you make your own scope. `CreateScope` starts one, you ask it for a fresh `AppDbContext`, and the `using` disposes both at the end of each run. Do this once per unit of work, not once for the whole loop.

## An in-memory email queue

The [Sending email](/web-api/email) page sends inside the request, so the client waits for the mail server. A queue fixes that. The endpoint drops a message in, returns at once, and a background worker sends it.

```csharp [Background/EmailQueue.cs]
using System.Threading.Channels;

namespace TodoApi.Background;

public record EmailMessage(string To, string Subject, string Body);

public class EmailQueue
{
    private readonly Channel<EmailMessage> channel = Channel.CreateBounded<EmailMessage>(100);

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);
}
```

`Channel<T>` is a built-in, thread-safe queue. Many requests can write to it while one worker reads. A bounded channel holds at most 100 messages. When it is full, `EnqueueAsync` waits for space instead of letting memory grow without limit.

```csharp [Background/EmailWorker.cs]
using TodoApi.Services;

namespace TodoApi.Background;

public class EmailWorker(EmailQueue queue, IServiceScopeFactory scopeFactory, ILogger<EmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await sender.SendAsync(message.To, message.Subject, message.Body, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Sending email to {To} failed", message.To);
            }
        }
    }
}
```

`await foreach` waits until a message arrives, handles it, then waits again. `IEmailSender` was registered as scoped, so the worker creates a scope for each message, the same way the cleanup job does.

## Register everything

```csharp [Program.cs (excerpt)]
using TodoApi.Background;

builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailWorker>();
builder.Services.AddHostedService<CleanupJob>();
```

The queue is a singleton so that endpoints and the worker share the same instance. `AddHostedService` tells the app to start the class at startup.

## Return 202 Accepted

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
// at the top of the file
using Microsoft.Extensions.Options;
using TodoApi.Background;
using TodoApi.Services;

// in MapTodoEndpoints
group.MapPost("/{id:int}/remind", Remind);

// in the TodoEndpoints class
static async Task<Results<Accepted, NotFound>> Remind(
    int id, AppDbContext db, EmailQueue queue, IOptions<EmailOptions> options)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return TypedResults.NotFound();
    }

    await queue.EnqueueAsync(new EmailMessage(
        options.Value.NotifyTo, "Reminder", $"Don't forget: {todo.Title}"));

    return TypedResults.Accepted($"/todos/{id}");
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
// at the top of the file
using Microsoft.Extensions.Options;
using TodoApi.Background;
using TodoApi.Services;

// add the queue to the primary constructor, keeping any services you already have
public class TodosController(
    AppDbContext db,
    EmailQueue queue,
    IOptions<EmailOptions> options) : ControllerBase

// in the TodosController class
[HttpPost("{id:int}/remind")]
public async Task<IActionResult> Remind(int id)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return NotFound();
    }

    await queue.EnqueueAsync(new EmailMessage(
        options.Value.NotifyTo, "Reminder", $"Don't forget: {todo.Title}"));

    return Accepted($"/todos/{id}");
}
```

`Accepted` sets the `Location` header to the URL you pass. A singleton such as `EmailQueue` is safe to take in a controller's constructor, because the controller only lives for one request.

</div>

`202 Accepted` means "I got your request and will finish it later". The response comes back in milliseconds, whether the mail server is fast, slow, or down. To do the same for the completed email, use `EmailQueue queue` in place of `IEmailSender email`, as an `Update` parameter in Minimal APIs or in the constructor for controllers, and replace the `email.SendAsync` line with `await queue.EnqueueAsync(new EmailMessage(options.Value.NotifyTo, "Todo completed", $"\"{todo.Title}\" is done."));`. Keep the `CompletedAt` line from above. The [Error handling](/web-api/errors) page shows the whole `Update` with these changes.

::: warning
The queue lives in memory. If the app restarts or crashes, every message still in the channel is lost, and a failed send is not retried. That is fine for reminders. When losing a job matters, use a persistent queue, covered on [Queues and retries](/web-api/queues).
:::

## Learn more

- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
- [Channels in .NET](https://learn.microsoft.com/dotnet/core/extensions/channels)
