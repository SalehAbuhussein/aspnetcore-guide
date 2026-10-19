# Rate limiting

## Introduction

Rate limiting caps how many requests a client can make in a period of time. It slows down password guessing and stops one visitor from flooding your forms. ASP.NET Core has a rate limiter built in, so you need no extra package.

## Adding a fixed window policy

```csharp [Program.cs (excerpt)]
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("create", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});
```

A policy is a named set of limits that you attach to endpoints. A fixed window limiter allows `PermitLimit` requests per `Window`, then rejects the rest until the window resets. This one allows 10 requests a minute. `QueueLimit = 0` rejects extra requests at once instead of holding them in a queue.

`RejectionStatusCode` sets the response for a rejected request. Use 429 Too Many Requests, the status code made for this. The default is 503 Service Unavailable, which wrongly tells clients that your server is down.

## Placing UseRateLimiter

```csharp [Program.cs (excerpt)]
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
```

`UseRateLimiter` must come after `UseRouting`. The limiter reads `[EnableRateLimiting]` from the matched endpoint, and the endpoint is only known once routing has run. Putting it before authentication also means rejected requests skip the more expensive steps. See [Middleware](/mvc/middleware) for why order matters.

## Protecting the Create action

```csharp [Controllers/BooksController.cs (excerpt)]
using Microsoft.AspNetCore.RateLimiting;

// POST: /Books/Create
[HttpPost]
[ValidateAntiForgeryToken]
[EnableRateLimiting("create")]
public async Task<IActionResult> Create(Book book, IFormFile? cover)
{
    // as before
}
```

`[EnableRateLimiting("create")]` applies the named policy to this action only. The GET action that shows the form is not limited. You can also put the attribute on a whole controller and turn it off for one action with `[DisableRateLimiting]`.

## Limiting per IP address

The `create` policy above has one shared counter for everyone. Ten requests from one visitor would block every other visitor for the rest of the minute. A partitioned limiter keeps a separate counter for each key, such as the client's IP address.

```csharp [Program.cs (excerpt)]
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // the "create" policy from above stays here

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

`AddPolicy` creates a named policy whose function picks a partition, a separate bucket of permits, for each request. Here the key is the IP address, so each address gets 5 tries a minute.

This is the same `AddRateLimiter` call as before, with two more settings. Keep one call and put every policy inside it.

`GlobalLimiter` applies to every request, before any named policy. `PartitionedRateLimiter.Create` builds it the same way, so each IP address gets 100 requests a minute across the whole site. To key signed-in users by user name instead, use `httpContext.User.Identity?.Name` as the key and move `UseRateLimiter` after `UseAuthentication`, because `User` is empty until authentication runs.

::: warning
Behind a reverse proxy or load balancer, `RemoteIpAddress` is the proxy's address, so every visitor shares one bucket. Turn on the forwarded headers middleware so ASP.NET Core reads the real client address. See [Deployment](/mvc/deployment).
:::

## Protecting the login page

If you wrote your own login action, add `[EnableRateLimiting("login")]` to its POST method. The Identity login page from [Authentication](/mvc/authentication) is a Razor Page inside a package, so you cannot add an attribute to it. Add the policy with a page convention instead.

```csharp [Program.cs (excerpt)]
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddAreaPageApplicationModelConvention(
        "Identity", "/Account/Login",
        model => model.EndpointMetadata.Add(new EnableRateLimitingAttribute("login")));
});
```

This attaches the same metadata the attribute would, so each IP address can load or submit the login page 5 times a minute.

## What the client sees

A rejected request gets status 429 and an empty body. Browsers show a blank page, which is fine for an attacker but unfriendly for a real person. Set `options.OnRejected` to write a short message, and add a `Retry-After` header when the limiter provides one.

```csharp [Program.cs (excerpt)]
options.OnRejected = async (context, ct) =>
{
    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
    {
        context.HttpContext.Response.Headers.RetryAfter =
            ((int)retryAfter.TotalSeconds).ToString();
    }

    await context.HttpContext.Response.WriteAsync("Too many requests. Try again in a minute.", ct);
};
```

Put this inside the `AddRateLimiter` lambda, next to `RejectionStatusCode`.

## Learn more

- [Rate limiting middleware in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/rate-limit)
