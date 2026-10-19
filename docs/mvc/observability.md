# Observability

## Introduction

When a user says "the book page was slow yesterday," logs alone rarely tell you why. Observability means your app emits logs, traces, and metrics that let you answer questions you did not plan for. You need it once the app runs in production with real users. For a local learning project, the console logs from [Logging](/mvc/logging) are enough.

## The three signals

Logs are timestamped messages about events. Traces follow one request through your app and show how long each step took, such as the controller action, the database query, and any outgoing HTTP call. Each step is a span. Metrics are numbers aggregated over time, such as requests per second or books created per hour.

.NET has all three built in. `ILogger` writes logs, `System.Diagnostics.ActivitySource` writes trace spans, and `System.Diagnostics.Metrics.Meter` records metrics. OpenTelemetry is the open standard that collects them and ships them to a backend.

## Structured logging and scopes

You already write structured logs with message templates, such as `logger.LogInformation("Loading book {BookId}", id)`. Each placeholder becomes a named field you can search on.

A log scope attaches fields to every message written inside a block.

```csharp [Controllers/BooksController.cs (excerpt)]
public async Task<IActionResult> Edit(int id, Book book)
{
    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["BookId"] = id,
        ["UserName"] = User.Identity?.Name ?? "anonymous"
    });

    logger.LogInformation("Saving changes");
    // ... every log line in this action now carries BookId and UserName ...
}
```

Scopes save you from repeating the same values in every message. The console logger hides them unless you turn them on.

```json [appsettings.Development.json (excerpt)]
{
  "Logging": {
    "Console": {
      "FormatterOptions": {
        "IncludeScopes": true
      }
    }
  }
}
```

## Installing OpenTelemetry

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

The hosting package wires OpenTelemetry into the app. The two instrumentation packages create spans and metrics for incoming requests and outgoing `HttpClient` calls. The exporter sends everything over OTLP, the OpenTelemetry protocol, which most backends accept.

## Configuring traces, metrics, and logs

First create one place for your own telemetry sources.

```csharp [Telemetry/BookShelfTelemetry.cs]
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BookShelf.Telemetry;

public static class BookShelfTelemetry
{
    public const string Name = "BookShelf";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    public static readonly Counter<long> BooksCreated =
        Meter.CreateCounter<long>("bookshelf.books.created", description: "Books added to the shelf");
}
```

Then register OpenTelemetry.

```csharp [Program.cs (excerpt)]
using BookShelf.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("BookShelf"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(BookShelfTelemetry.Name))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(BookShelfTelemetry.Name))
    .UseOtlpExporter();

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
});
```

`AddService` names your app in the backend. `AddSource` and `AddMeter` subscribe to your own sources by name. Anything not subscribed is ignored, so a typo here means your custom data never shows up. `UseOtlpExporter` sends traces, metrics, and logs to one OTLP endpoint. `builder.Logging.AddOpenTelemetry` sends your `ILogger` output there as well, linked to the trace of the request that wrote it.

The exporter reads its target from the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable and defaults to `http://localhost:4317`.

## A custom span and counter

```csharp [Controllers/BooksController.cs (excerpt)]
using BookShelf.Telemetry;

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(Book book)
{
    if (!ModelState.IsValid) return View(book);

    using var activity = BookShelfTelemetry.ActivitySource.StartActivity("CreateBook");
    activity?.SetTag("book.title", book.Title);

    db.Books.Add(book);
    await db.SaveChangesAsync();

    activity?.SetTag("book.id", book.Id);
    BookShelfTelemetry.BooksCreated.Add(1);

    return RedirectToAction(nameof(Index));
}
```

`StartActivity` opens a span that ends when `activity` is disposed. It returns `null` when nobody is listening, which is why the code uses `?.`. The span nests under the request span from ASP.NET Core automatically. `BooksCreated.Add(1)` bumps the counter, and the backend can graph it per minute or per hour.

::: tip
Keep tag values small and avoid personal data. Do not put a user's email in a span tag or a metric dimension, because traces often end up in third-party tools.
:::

## Viewing it locally with the Aspire dashboard

The .NET Aspire dashboard is a free viewer for OTLP data. It runs as a container and needs no Aspire project.

```bash
docker run --rm -it -p 18888:18888 -p 4317:18889 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard
```

Port 18888 serves the dashboard UI, and port 4317 on your machine forwards to the dashboard's OTLP receiver. The container prints a login URL with a token in its output. Open that URL, then run BookShelf with `dotnet run` and click around.

The Traces page shows each request as a timeline, with your `CreateBook` span inside it. The Metrics page lists `bookshelf.books.created` next to the built-in HTTP metrics. The Structured logs page shows your log lines with their scope fields, and each one links to its trace.

The dashboard keeps data in memory only. In production, point `OTEL_EXPORTER_OTLP_ENDPOINT` at a real backend such as Azure Monitor, Grafana, Honeycomb, or an OpenTelemetry Collector. Your code does not change.

## Learn more

- [.NET observability with OpenTelemetry](https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel)
- [Standalone Aspire dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone)
