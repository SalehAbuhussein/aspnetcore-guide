# Calling other APIs

## Introduction

Sooner or later your API calls someone else's: a payment provider, a geocoder, a public data feed. Those calls are slow, sometimes fail, and can exhaust your server's connections if you create clients the wrong way. This page builds a typed client with `IHttpClientFactory` and adds retries and timeouts. You need it the first time your API makes an outgoing HTTP call.

The example checks whether a date is a public holiday using the free Nager.Date API, so `TodoApi` can warn when a due date lands on one.

<ApiStyle />

## Why not new HttpClient

```csharp
// Do not do this in a handler
using var client = new HttpClient();
var json = await client.GetStringAsync(url);
```

Each `HttpClient` opens its own connections. Disposing it does not close the socket right away, so creating one per request under load runs the server out of sockets. Keeping one static `HttpClient` forever has the opposite problem: it never notices DNS changes. `IHttpClientFactory` pools the underlying handlers and recycles them every few minutes, which avoids both.

## The response type

`GET https://date.nager.at/api/v3/PublicHolidays/2026/US` returns a JSON array like this:

```json
[
  { "date": "2026-01-01", "localName": "New Year's Day", "name": "New Year's Day", "countryCode": "US" }
]
```

```csharp [Holidays/PublicHoliday.cs]
namespace TodoApi.Holidays;

public record PublicHoliday(DateOnly Date, string LocalName, string Name);
```

You only declare the properties you use. `System.Text.Json` ignores the rest and reads `"2026-01-01"` into a `DateOnly`.

## A typed client

```csharp [Holidays/HolidayClient.cs]
namespace TodoApi.Holidays;

public class HolidayClient(HttpClient http)
{
    public async Task<List<PublicHoliday>> GetAsync(int year, string countryCode, CancellationToken ct)
    {
        var holidays = await http.GetFromJsonAsync<List<PublicHoliday>>(
            $"api/v3/PublicHolidays/{year}/{countryCode}", ct);

        return holidays ?? [];
    }

    public async Task<PublicHoliday?> FindAsync(DateOnly date, string countryCode, CancellationToken ct)
    {
        var holidays = await GetAsync(date.Year, countryCode, ct);
        return holidays.FirstOrDefault(h => h.Date == date);
    }
}
```

A typed client is a class that receives a configured `HttpClient` in its constructor and wraps the calls to one API. The rest of your code calls `FindAsync` and never sees URLs or JSON. `GetFromJsonAsync` sends the `GET`, checks for a success status code, and deserializes the body with camelCase names.

## Registering it

```bash
dotnet add package Microsoft.Extensions.Http.Resilience
```

```csharp [Program.cs (excerpt)]
using TodoApi.Holidays;

builder.Services.AddHttpClient<HolidayClient>(client =>
{
    client.BaseAddress = new Uri("https://date.nager.at/");
})
.AddStandardResilienceHandler();
```

`AddHttpClient<HolidayClient>` registers the typed client and tells the factory how to configure its `HttpClient`. Keep the trailing slash on `BaseAddress` so relative paths are appended correctly.

`AddStandardResilienceHandler` adds a tested set of rules around every call:

| Rule | Default |
| --- | --- |
| Total request timeout | 30 seconds, including retries |
| Retry | 3 retries with growing, randomized delays, for 5xx, 408, 429 and network errors |
| Circuit breaker | Stops calling for a while when most recent calls failed |
| Attempt timeout | 10 seconds per attempt |

The circuit breaker matters when the other API is down. Instead of every request waiting 30 seconds to fail, calls fail fast until the API recovers.

## Timeouts

`HttpClient.Timeout` defaults to 100 seconds, which is far too long for a call made inside a user's request. With the resilience handler, change its options instead of `HttpClient.Timeout`:

```csharp [Program.cs (excerpt)]
builder.Services.AddHttpClient<HolidayClient>(client =>
{
    client.BaseAddress = new Uri("https://date.nager.at/");
})
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
});
```

Pick timeouts based on how long your user can wait. Also pass the request's `CancellationToken`, as shown on [Async and cancellation](/web-api/async), so the call stops when the client leaves.

::: warning
Retries resend the request. That is safe for `GET`, but a retried `POST` to a payment API can charge twice. For non-idempotent calls, use an idempotency key if the other API supports one, or turn retries off for that client.
:::

## Using it in an endpoint

<div class="only-minimal">

```csharp [Endpoints/HolidayEndpoints.cs]
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Holidays;

namespace TodoApi.Endpoints;

public static class HolidayEndpoints
{
    public static void MapHolidayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/holidays/check", Check);
    }

    static async Task<Ok<HolidayCheck>> Check(
        DateOnly date, string country, HolidayClient holidays, CancellationToken ct)
    {
        var holiday = await holidays.FindAsync(date, country, ct);
        return TypedResults.Ok(new HolidayCheck(date, holiday is not null, holiday?.Name));
    }
}

public record HolidayCheck(DateOnly Date, bool IsHoliday, string? Name);
```

```csharp [Program.cs (excerpt)]
app.MapHolidayEndpoints();
```

</div>

<div class="only-controllers">

```csharp [Controllers/HolidaysController.cs]
using Microsoft.AspNetCore.Mvc;
using TodoApi.Holidays;

namespace TodoApi.Controllers;

[ApiController]
[Route("holidays")]
public class HolidaysController(HolidayClient holidays) : ControllerBase
{
    [HttpGet("check")]
    public async Task<ActionResult<HolidayCheck>> Check(
        [FromQuery] DateOnly date, [FromQuery] string country, CancellationToken ct)
    {
        var holiday = await holidays.FindAsync(date, country, ct);
        return Ok(new HolidayCheck(date, holiday is not null, holiday?.Name));
    }
}

public record HolidayCheck(DateOnly Date, bool IsHoliday, string? Name);
```

`MapControllers()` in `Program.cs` finds the new controller, so there is nothing else to register. `[FromQuery]` reads `date` and `country` from the query string.

</div>

`GET /holidays/check?date=2026-12-25&country=US` returns `{ "date": "2026-12-25", "isHoliday": true, "name": "Christmas Day" }`. A client can call it before saving a due date and show a warning. The typed client is injected like any other service.

The holiday list for a year rarely changes, so this is a good candidate for [caching](/web-api/caching). Wrap `GetAsync` in `HybridCache.GetOrCreateAsync` with a key like `holidays:2026:US` and a long expiration.

## Learn more

- [Make HTTP requests using IHttpClientFactory in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/http-requests)
- [Build resilient HTTP apps](https://learn.microsoft.com/dotnet/core/resilience/http-resilience)
