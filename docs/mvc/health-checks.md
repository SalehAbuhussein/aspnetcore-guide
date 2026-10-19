# Health checks

## Introduction

Once BookShelf runs on a server, something other than you needs to know whether it is working. Load balancers, container platforms, and uptime monitors ask a health endpoint, and they stop sending traffic or restart the app when it reports a problem. Add health checks as soon as the app runs behind a load balancer or in containers. On a hobby app with one server and no monitor, they add little.

## A basic health endpoint

```csharp [Program.cs (excerpt)]
builder.Services.AddHealthChecks();

var app = builder.Build();

// ... existing middleware ...

app.MapHealthChecks("/health");
```

`AddHealthChecks` registers the health check services, and it ships with ASP.NET Core. `MapHealthChecks` adds an endpoint that runs every registered check and returns the overall result. With no checks registered, it answers `Healthy` with status 200 as long as the app can serve requests.

```bash
curl -i http://localhost:5000/health
```

The body is plain text: `Healthy`, `Degraded`, or `Unhealthy`. `Healthy` and `Degraded` return 200, and `Unhealthy` returns 503. Most tools only look at the status code.

## Checking the database

```bash
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
```

```csharp [Program.cs (excerpt)]
using BookShelf.Data;

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();
```

`AddDbContextCheck` asks `AppDbContext` whether it can connect to the database. If the database file is missing or the server is down, the check fails and `/health` returns 503.

## Writing your own check

```csharp [HealthChecks/DiskSpaceHealthCheck.cs]
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BookShelf.HealthChecks;

public class DiskSpaceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var drive = new DriveInfo(AppContext.BaseDirectory);
        var freeGb = drive.AvailableFreeSpace / 1024 / 1024 / 1024;

        return Task.FromResult(freeGb switch
        {
            < 1 => HealthCheckResult.Unhealthy($"Only {freeGb} GB free"),
            < 5 => HealthCheckResult.Degraded($"{freeGb} GB free"),
            _ => HealthCheckResult.Healthy(),
        });
    }
}
```

A check is a class that implements `IHealthCheck`. Register it with `.AddCheck<DiskSpaceHealthCheck>("disk")`. Keep checks fast and cheap, because monitors call them every few seconds.

## Liveness and readiness

A single endpoint mixes two questions. Liveness asks "is the process alive, or is it stuck?" Readiness asks "can it serve real traffic right now?" The answers lead to different actions. A failed liveness check means restart the app. A failed readiness check means stop sending it traffic for a while, but leave it running.

If the database goes down and your liveness check includes the database, the platform restarts every copy of your app in a loop, and restarting does not fix the database. So the liveness endpoint should run no dependency checks, and the readiness endpoint should run them all.

Use tags to split them.

```csharp [Program.cs (excerpt)]
using BookShelf.Data;
using BookShelf.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"])
    .AddCheck<DiskSpaceHealthCheck>("disk", tags: ["ready"]);

var app = builder.Build();

// ... existing middleware ...

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

`Predicate` picks which checks an endpoint runs. `_ => false` runs none, so `/health/live` returns 200 whenever the app can answer HTTP at all. `/health/ready` runs only checks tagged `ready`.

::: warning
If you set a fallback authorization policy that requires sign-in, health endpoints require it too, and every probe gets a redirect. Add `.AllowAnonymous()` after `MapHealthChecks`. Do not put secrets in check descriptions, because anyone can read the response.
:::

## What load balancers do with it

A load balancer, such as Azure Application Gateway, AWS ALB, or Nginx Plus, calls the readiness endpoint on each server every few seconds. When a server returns 503 a few times in a row, the balancer stops routing users to it. When it returns 200 again, traffic comes back. Point the balancer at `/health/ready`.

Kubernetes uses both endpoints. Set `livenessProbe` to `/health/live` and `readinessProbe` to `/health/ready`. Kubernetes restarts the container on liveness failures and removes it from the service on readiness failures.

## Docker HEALTHCHECK

```dockerfile [Dockerfile (excerpt)]
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD curl --fail http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "BookShelf.dll"]
```

`HEALTHCHECK` makes Docker run the command on a timer and mark the container `healthy` or `unhealthy`. You see the state in `docker ps`. Docker Compose can wait for a healthy container with `depends_on` and `condition: service_healthy`, and Docker Swarm replaces unhealthy containers.

The ASP.NET Core runtime image does not include `curl`, which is why the excerpt installs it. Plain Docker only reports the status and does not restart an unhealthy container. Kubernetes ignores `HEALTHCHECK` entirely and uses its own probes. This Dockerfile builds on the one from [Deployment](/mvc/deployment).

## Learn more

- [Health checks in ASP.NET Core](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
