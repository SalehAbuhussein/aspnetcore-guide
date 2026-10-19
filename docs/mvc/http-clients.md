# Calling other APIs

## Introduction

Sooner or later your app needs data from another service, such as book details, prices, or a payment provider. Calling it looks like one line of code, but done carelessly it can exhaust the server's connections or hang a page when the other side is slow. This page builds a typed client for Open Library with `IHttpClientFactory`, adds timeouts and retries, and explains what to avoid. You need it the first time your app calls out over HTTP.

## Never new up HttpClient per request

```csharp
// Do not do this.
using var client = new HttpClient();
var json = await client.GetStringAsync("https://openlibrary.org/isbn/9780140328721.json");
```

Each `HttpClient` opens its own network connections. When you dispose it, the operating system keeps each closed connection's socket reserved for a few minutes before it can be reused. Under load, a new client per request piles up thousands of these waiting sockets until the server runs out and every outgoing call fails. That is socket exhaustion. The opposite mistake, one static `HttpClient` forever, never notices DNS changes. `IHttpClientFactory` solves both by pooling and recycling the underlying connections for you.

## A typed client

A typed client is a class that wraps `HttpClient` for one external service. The rest of your app calls its methods and never sees URLs or JSON.

```csharp [Services/OpenLibraryClient.cs]
using System.Net;
using System.Text.Json.Serialization;

namespace BookShelf.Services;

public record OpenLibraryBook(
    string Title,
    [property: JsonPropertyName("publish_date")] string? PublishDate,
    [property: JsonPropertyName("number_of_pages")] int? NumberOfPages);

public class OpenLibraryClient(HttpClient http)
{
    public async Task<OpenLibraryBook?> GetByIsbnAsync(string isbn, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"isbn/{Uri.EscapeDataString(isbn)}.json", ct);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OpenLibraryBook>(ct);
    }
}
```

The client receives an `HttpClient` through its constructor, already set up by the factory. Open Library returns 404 for an unknown ISBN, so the method turns that into `null` and throws for any other error. `JsonPropertyName` maps the snake_case JSON names to C# properties.

When you do not need to handle a 404, `GetFromJsonAsync` is shorter.

```csharp
var book = await http.GetFromJsonAsync<OpenLibraryBook>($"isbn/{isbn}.json", ct);
```

It sends the request, throws on any non-success status, and reads the JSON in one call.

## Registering the client

```csharp [Program.cs (excerpt)]
using BookShelf.Services;

builder.Services.AddHttpClient<OpenLibraryClient>(client =>
{
    client.BaseAddress = new Uri("https://openlibrary.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BookShelf/1.0");
});
```

`AddHttpClient<T>` registers `OpenLibraryClient` as a transient service and configures the `HttpClient` it receives. Keep the trailing slash on `BaseAddress`, and no leading slash on relative paths like `isbn/...`, or part of the URL gets dropped. Open Library asks callers to send a `User-Agent` that names the app.

## Using it in a controller

```csharp [Controllers/BooksController.cs (excerpt)]
public async Task<IActionResult> Lookup(string isbn, [FromServices] OpenLibraryClient openLibrary,
    CancellationToken ct)
{
    var found = await openLibrary.GetByIsbnAsync(isbn, ct);
    if (found is null) return NotFound();

    var book = new Book { Title = found.Title };
    return View("Create", book);
}
```

This fills the Create form with the title from Open Library. `[FromServices]` injects the client into one action instead of the whole controller. Pass the request's [cancellation token](/mvc/async) so the call stops when the user leaves.

## Timeouts

By default `HttpClient` waits 100 seconds before giving up. A page that hangs that long is broken. Set a shorter timeout through the resilience handler below, or on the client with `client.Timeout = TimeSpan.FromSeconds(10)` if you skip resilience.

## Retries and resilience

Networks fail for a second here and there. A resilience handler retries those failures and stops calling a service that is clearly down.

```bash
dotnet add package Microsoft.Extensions.Http.Resilience
```

```csharp [Program.cs (excerpt)]
builder.Services.AddHttpClient<OpenLibraryClient>(client =>
{
    client.BaseAddress = new Uri("https://openlibrary.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BookShelf/1.0");
})
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
});
```

`AddStandardResilienceHandler` adds sensible defaults in one call. It retries failed requests up to three times with growing delays, times out each attempt, caps the total time, and opens a circuit breaker when too many calls fail so your app stops waiting on a dead service. Here each attempt gets 5 seconds and the whole call, retries included, gets 15.

::: warning
Retries are safe for GET requests. For a POST that charges a card or creates an order, a retry can do the action twice. Only retry those when the other service supports an idempotency key.
:::

## Learn more

- [Make HTTP requests using IHttpClientFactory](https://learn.microsoft.com/aspnet/core/fundamentals/http-requests)
- [Build resilient HTTP apps](https://learn.microsoft.com/dotnet/core/resilience/http-resilience)
