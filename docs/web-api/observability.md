# Observability

## Introduction

When a request is slow or fails in production, logs alone rarely tell you why. Observability means collecting traces, metrics, and logs so you can follow one request through your API, its database calls, and the services it calls. You need it once your API runs on servers you cannot attach a debugger to. For a local learning project, the console log is enough.

<ApiStyle />

## The three signals

- A trace records one request as a tree of timed steps, called spans or activities.
- A metric is a number measured over time, such as requests per second or todos created.
- A log is a message tied to a moment, ideally tagged with the trace it belongs to.

OpenTelemetry is the open standard for all three. .NET produces the data through `Activity` and `Meter`, and the OpenTelemetry SDK collects it and sends it to a backend using OTLP, its wire protocol.

## Installing the packages

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

`Extensions.Hosting` wires OpenTelemetry into the app. The two `Instrumentation` packages record incoming requests and outgoing `HttpClient` calls. The exporter sends everything over OTLP.

## Configuring OpenTelemetry

```csharp [Program.cs (excerpt)]
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TodoApi.Telemetry;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("TodoApi"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(TodoTelemetry.SourceName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(TodoTelemetry.MeterName))
    .WithLogging()
    .UseOtlpExporter();
```

`AddService("TodoApi")` names your app in the backend. `WithTracing` and `WithMetrics` choose which sources to listen to. `WithLogging` sends your existing `ILogger` output through OpenTelemetry too. `UseOtlpExporter` sends all three signals to the endpoint in the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable, or `http://localhost:4317` when it is not set.

## Viewing data locally with the Aspire dashboard

```bash
docker run --rm -it -p 18888:18888 -p 4317:18889 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

The Aspire dashboard is a free container that receives OTLP data and shows traces, metrics, and structured logs in a browser. Port `4317` on your machine maps to the dashboard's OTLP port, which matches the exporter default. Open `http://localhost:18888` and use the login link printed in the container output.

Run the API, send a few requests, and open the Traces tab. Each request shows its ASP.NET Core span, and any `HttpClient` call it made appears as a child span under it.

## Custom traces and metrics

```csharp [Telemetry/TodoTelemetry.cs]
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TodoApi.Telemetry;

public static class TodoTelemetry
{
    public const string SourceName = "TodoApi";
    public const string MeterName = "TodoApi";

    public static readonly ActivitySource Source = new(SourceName);

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> TodosCreated =
        Meter.CreateCounter<long>("todos.created");
}
```

An `ActivitySource` creates spans, and a `Meter` creates instruments such as counters. The names must match the `AddSource` and `AddMeter` calls in `Program.cs`, or OpenTelemetry ignores them.

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
using TodoApi.Telemetry;

static async Task<Created<Todo>> Create(CreateTodo input, AppDbContext db)
{
    using var activity = TodoTelemetry.Source.StartActivity("CreateTodo");

    var todo = new Todo { Title = input.Title };
    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    activity?.SetTag("todo.id", todo.Id);
    TodoTelemetry.TodosCreated.Add(1);

    return TypedResults.Created($"/todos/{todo.Id}", todo);
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using TodoApi.Telemetry;

[HttpPost]
public async Task<ActionResult<Todo>> Create(CreateTodo input)
{
    using var activity = TodoTelemetry.Source.StartActivity("CreateTodo");

    var todo = new Todo { Title = input.Title };
    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    activity?.SetTag("todo.id", todo.Id);
    TodoTelemetry.TodosCreated.Add(1);

    return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
}
```

</div>

`StartActivity` returns `null` when nobody is listening, so use `?.` on it. The span ends when `activity` is disposed at the end of the method. `TodosCreated.Add(1)` bumps the counter, which shows up in the dashboard's Metrics tab as `todos.created`.

::: tip
In code that takes dependencies, inject `IMeterFactory` and create the `Meter` from it instead of a static field. It plays better with tests. The static class keeps this example short.
:::

## Correlating logs with traces

Every request gets a trace id, and ASP.NET Core attaches it to the logging scope. With `WithLogging()`, each log record you write with `ILogger` carries that trace id and span id, so the dashboard links a log line to the request that produced it.

The same id appears in error responses. `AddProblemDetails` from the [Errors](/web-api/errors) page adds a `traceId` field to every ProblemDetails body. When a user reports an error, ask for that value and search for it.

```csharp [Program.cs (excerpt)]
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
```

If you also read raw console logs, this prints each log line as JSON with `TraceId` and `SpanId` in its scope. See [Logging](/web-api/logging) for the logging basics.

## Learn more

- [.NET observability with OpenTelemetry](https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel)
- [Creating metrics](https://learn.microsoft.com/dotnet/core/diagnostics/metrics-instrumentation)
