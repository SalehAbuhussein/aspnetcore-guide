# Health checks

## Introduction

Load balancers, Docker, and Kubernetes need a way to ask your API "are you working?" so they can stop sending traffic to a broken instance or restart it. A health check endpoint answers that question. You need one as soon as you run behind any of those. A local hobby app does not.

## A basic health endpoint

```csharp [Program.cs (excerpt)]
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
```

`AddHealthChecks` registers the health check service, and `MapHealthChecks` exposes it as an endpoint. With no checks added, `GET /health` returns `200` with the plain text body `Healthy` as long as the app can serve requests. If any check reports `Unhealthy`, the endpoint returns `503 Service Unavailable` with the body `Unhealthy`.

## Checking the database

```bash
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
```

```csharp [Program.cs (excerpt)]
using TodoApi.Data;

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();
```

`AddDbContextCheck` asks `AppDbContext` whether it can connect to the database. If the database file is missing or the server is down, the check fails and `/health` returns `503`.

For other dependencies, write a class that implements `IHealthCheck` and register it with `AddCheck<T>("name")`. Its `CheckHealthAsync` method returns `HealthCheckResult.Healthy()`, `Degraded()`, or `Unhealthy()`.

## Liveness and readiness

One endpoint mixes two questions that need different answers.

- Liveness asks "is the process stuck?" A failure means restart it.
- Readiness asks "can it serve requests right now?" A failure means stop sending traffic until it recovers.

If the database goes down, every instance fails its database check. Restarting them all does not fix the database, so the database check belongs in readiness only. Split them with tags.

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TodoApi.Data;

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

A tag is a label you attach to a check. `Predicate` decides which checks an endpoint runs. The live endpoint runs none, so it returns `200` whenever the app can answer HTTP at all. The ready endpoint runs only the checks tagged `ready`.

## Keeping health endpoints open

```csharp [Program.cs (excerpt)]
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
})
.AllowAnonymous()
.DisableRateLimiting();
```

Orchestrators call these endpoints every few seconds without a token. If you set a fallback authorization policy on the [Authorization](/web-api/authorization) page, `AllowAnonymous` lets probes through. `DisableRateLimiting` stops a global [rate limiter](/web-api/rate-limiting) from turning a healthy instance into a "failed" one under load. Do the same for `/health/ready`.

::: warning
Health endpoints are public, so keep the response short. The default plain text body is fine. Do not write a custom response that lists connection strings, exception messages, or server names.
:::

## What load balancers and orchestrators do with it

A load balancer, such as Azure App Service health check or an AWS target group, calls the URL you configure on each instance. If it keeps failing, that instance is taken out of rotation until it passes again. Point it at `/health/ready`.

Kubernetes uses both endpoints.

```yaml [k8s/deployment.yaml (excerpt)]
containers:
  - name: todoapi
    image: ghcr.io/your-name/todoapi:latest
    ports:
      - containerPort: 8080
    livenessProbe:
      httpGet:
        path: /health/live
        port: 8080
      periodSeconds: 10
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 8080
      periodSeconds: 5
```

A failed liveness probe restarts the container. A failed readiness probe removes the pod from the service until it passes. Port `8080` is the default HTTP port in .NET container images.

Docker Compose and plain Docker support a `healthcheck` that runs a command inside the container. The default .NET images do not include `curl`, and the chiseled images have no shell at all, so most teams rely on the orchestrator's HTTP probes instead of a Docker `HEALTHCHECK`. See [Deployment](/web-api/deployment) for building the image.

## Learn more

- [Health checks in ASP.NET Core](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
