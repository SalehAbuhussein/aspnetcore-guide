# Background tasks

## Introduction

Some work should not happen inside a web request. This page runs an hourly cleanup job, sends email from a queue so the user does not wait, and shows how to use `AppDbContext` from code that runs outside a request.

## A hosted service

A hosted service is a class that starts with the app and runs alongside it. The easiest way to write one is to inherit from `BackgroundService` and override `ExecuteAsync`.

```csharp [Services/UploadCleanupService.cs]
using BookShelf.Data;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public class UploadCleanupService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment env,
    ILogger<UploadCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanUpAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Upload cleanup failed");
            }
        }
    }

    private async Task CleanUpAsync(CancellationToken ct)
    {
        var folder = Path.Combine(env.WebRootPath, "uploads");
        if (!Directory.Exists(folder)) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inUse = await db.Books
            .Where(b => b.CoverPath != null)
            .Select(b => b.CoverPath!)
            .ToListAsync(ct);
        var inUseNames = inUse.Select(p => Path.GetFileName(p)).ToHashSet();

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var old = File.GetCreationTimeUtc(file) < DateTime.UtcNow.AddDays(-1);
            if (old && !inUseNames.Contains(Path.GetFileName(file)))
            {
                File.Delete(file);
                logger.LogInformation("Deleted unused upload {File}", file);
            }
        }
    }
}
```

`PeriodicTimer` waits for the next tick and then lets the loop run once. When the app shuts down, `stoppingToken` is cancelled and `WaitForNextTickAsync` throws `OperationCanceledException`. `BackgroundService` treats that as a normal stop, so the loop ends cleanly. The job deletes cover images from the [File uploads](/mvc/file-uploads) page that no book points at and that are more than a day old.

The `try`/`catch` matters. If an exception escapes `ExecuteAsync`, ASP.NET Core stops the whole app by default.

```csharp [Program.cs (excerpt)]
builder.Services.AddHostedService<UploadCleanupService>();
```

`AddHostedService` registers the class as a singleton and starts it when the app starts.

## Scoped services in a background service

A hosted service is a singleton, and `AppDbContext` is scoped. A singleton must not hold a scoped service, as the [Dependency injection](/mvc/dependency-injection) page warns. So the cleanup job asks for `IServiceScopeFactory` instead, and creates a new scope for each run with `CreateScope`. The scope acts like a mini request. Anything you get from `scope.ServiceProvider` is disposed when the `using` block ends.

## Sending email without blocking the request

On the [Sending email](/mvc/email) page, the Create action waits for the SMTP server. Move that work onto a queue. The action adds a message and returns at once, and a background service sends it.

```csharp [Services/EmailQueue.cs]
using System.Threading.Channels;

namespace BookShelf.Services;

public record EmailMessage(string To, string Subject, string Body);

public class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();

    public ValueTask EnqueueAsync(EmailMessage message) =>
        _channel.Writer.WriteAsync(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
```

A `Channel<T>` is a thread-safe, in-memory queue built into .NET. One side writes items and the other side reads them as they arrive.

```csharp [Services/EmailWorker.cs]
namespace BookShelf.Services;

public class EmailWorker(
    EmailQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await sender.SendAsync(message.To, message.Subject, message.Body);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send email to {To}", message.To);
            }
        }
    }
}
```

`await foreach` waits for the next message and handles them one at a time. Register the queue as a singleton, so the controller and the worker share the same one.

```csharp [Program.cs (excerpt)]
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailWorker>();
```

Then change the controller to use the queue instead of `IEmailSender`.

```csharp [Controllers/BooksController.cs (excerpt)]
public class BooksController(
    AppDbContext db,
    IWebHostEnvironment env,
    EmailQueue emailQueue,
    IOptions<SmtpOptions> smtp) : Controller
{
    // POST: /Books/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Create(Book book, IFormFile? cover)
    {
        // The cover checks, the ModelState check, and the
        // SaveCoverAsync call from the File uploads page stay here.

        db.Books.Add(book);
        await db.SaveChangesAsync();

        await emailQueue.EnqueueAsync(new EmailMessage(
            smtp.Value.NotifyAddress,
            $"New book: {book.Title}",
            $"{book.Title} by {book.Author} ({book.Year}) was added to BookShelf."));

        return RedirectToAction(nameof(Index));
    }
}
```

The request now finishes as soon as the message is in the queue. A slow or broken mail server no longer affects the user.

::: warning
The queue lives in memory. If the app restarts or crashes, any message still in the queue is lost, and a failed send is not retried. That is fine for a "new book" notice. For work that must happen, such as a password reset email or a payment, use a durable queue. See [Queues](/mvc/queues).
:::

## Learn more

- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
- [Channels in .NET](https://learn.microsoft.com/dotnet/core/extensions/channels)
