# Rate limiting

## Introduction

Rate limiting caps how many requests a client can make in a period of time. It protects your API from abuse and from one busy client slowing everyone else down. ASP.NET Core includes rate limiting middleware in the `Microsoft.AspNetCore.RateLimiting` namespace, so there is no package to install.

<ApiStyle />

## Adding a fixed window limiter

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromSeconds(10);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

app.UseRateLimiter();
```

A fixed window limiter allows a set number of requests per time window, then resets when the window ends. This policy, named `fixed`, allows 10 requests every 10 seconds. `QueueLimit = 0` means extra requests are rejected right away instead of waiting for the next window.

`RejectionStatusCode` sets the status for rejected requests. The default is `503 Service Unavailable`, but `429 Too Many Requests` is the correct code and tells clients to slow down.

`UseRateLimiter()` adds the [middleware](/web-api/middleware). Place it after `UseAuthentication` and `UseAuthorization` if you have them, so the limiter can see who the user is.

## Applying the limiter to endpoints

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs]
public static void MapTodoEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/todos")
        .RequireRateLimiting("fixed");

    group.MapGet("/", GetAll);
    group.MapGet("/{id:int}", GetById);
    group.MapPost("/", Create);
    group.MapPut("/{id:int}", Update);
    group.MapDelete("/{id:int}", Delete);
}
```

`RequireRateLimiting("fixed")` applies the named policy to every endpoint in the group. Call it on a single endpoint to limit only that one. To exclude one endpoint inside a limited group, call `DisableRateLimiting()` on it.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using Microsoft.AspNetCore.RateLimiting;

namespace TodoApi.Controllers;

[ApiController]
[Route("todos")]
[EnableRateLimiting("fixed")]
public class TodosController(AppDbContext db) : ControllerBase
{
    // GetAll, GetById, Create, Update, and Delete stay as they are
}
```

`[EnableRateLimiting("fixed")]` on the controller class applies the named policy to every action in it. Put it on a single action to limit only that one. To exclude one action inside a limited controller, add `[DisableRateLimiting]` to it.

</div>

::: warning
A named policy like `fixed` shares one counter across all clients. Ten requests from any mix of users fill the window for everyone. Use a partitioned limiter, shown below, to count each client separately.
:::

## Testing the limit

```http [TodoApi.http]
@TodoApi_HostAddress = http://localhost:5000

GET {{TodoApi_HostAddress}}/todos
```

Send the request more than 10 times within 10 seconds. The eleventh response is `429 Too Many Requests`. Wait for the window to end and requests succeed again.

## Other limiter types

```csharp [Program.cs]
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddSlidingWindowLimiter("sliding", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.SegmentsPerWindow = 6;
    });

    options.AddTokenBucketLimiter("bucket", limiter =>
    {
        limiter.TokenLimit = 20;
        limiter.TokensPerPeriod = 5;
        limiter.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
    });

    options.AddConcurrencyLimiter("concurrent", limiter =>
    {
        limiter.PermitLimit = 5;
    });
});
```

A sliding window splits the window into segments, which smooths out bursts at window edges. A token bucket refills a fixed number of tokens each period, and each request spends one. A concurrency limiter caps how many requests run at the same time, rather than per period.

## Limiting per client

```csharp [Program.cs]
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("per-ip", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromSeconds(10)
            }));
});
```

A partition is a separate counter for one key. Here the key is the client's IP address, so each address gets its own 10 requests.

<div class="only-minimal">

Use `context.User.Identity?.Name` as the key instead to limit signed-in users one by one. Apply it with `RequireRateLimiting("per-ip")`.

</div>

<div class="only-controllers">

Use `context.User.Identity?.Name` as the key instead to limit signed-in users one by one. Apply it with `[EnableRateLimiting("per-ip")]`.

</div>

## Learn more

- [Rate limiting middleware in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/rate-limit)
