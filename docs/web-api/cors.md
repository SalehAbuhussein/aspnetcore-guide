# CORS

## Introduction

CORS, or Cross-Origin Resource Sharing, is a browser rule about which websites may call your API from JavaScript. If your frontend runs on a different address than your API, you need to turn on CORS for that address.

<ApiStyle />

## What CORS is

An origin is the scheme, host, and port of a URL, such as `http://localhost:4200`. By default, browsers block JavaScript on one origin from reading responses from another origin. CORS is how a server tells the browser "this other origin is allowed."

The check happens in the browser only. Tools like curl, Postman, and `.http` files ignore CORS, so a request can work there and still fail in your frontend.

For some requests, the browser first sends a preflight request. A preflight is an `OPTIONS` request that asks the server which methods and headers are allowed before sending the real request. ASP.NET Core answers preflight requests for you once CORS is set up.

## Adding a named policy

```csharp [Program.cs (excerpt)]
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("Frontend");
```

`AddCors` registers the CORS services and defines a policy named `Frontend`. The policy allows one origin, the default address of the Angular dev server (`ng serve`), with any header and any HTTP method. `UseCors("Frontend")` applies that policy to every endpoint.

Write the origin without a trailing slash. `http://localhost:4200/` does not match.

## Where UseCors goes

<div class="only-minimal">

```csharp [Program.cs]
var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapTodoEndpoints();
```

</div>

<div class="only-controllers">

```csharp [Program.cs]
var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
```

</div>

Middleware runs in the order you add it, so placement matters. Call `UseCors` before `UseAuthentication` and `UseAuthorization`. That way, preflight requests get their CORS headers before any auth check can reject them. See [Middleware](/web-api/middleware) for more on ordering.

## Applying CORS per endpoint

<div class="only-minimal">

```csharp [Program.cs]
app.UseCors();

app.MapTodoEndpoints();

app.MapGet("/public/stats", () => new { Count = 42 })
    .RequireCors("Frontend");
```

Call `UseCors()` with no policy name, then attach a policy to specific endpoints or groups with `RequireCors`. Endpoints without `RequireCors` get no CORS headers, so browsers on other origins cannot read them. This is useful when only part of your API is meant for the browser app.

</div>

<div class="only-controllers">

```csharp [Program.cs]
app.UseCors();

app.MapControllers();
```

```csharp [Controllers/StatsController.cs]
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Route("public/stats")]
[EnableCors("Frontend")]
public class StatsController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { Count = 42 });
}
```

Call `UseCors()` with no policy name, then attach a policy to specific controllers or actions with `[EnableCors("Frontend")]`. Actions without it get no CORS headers, so browsers on other origins cannot read them. This is useful when only part of your API is meant for the browser app. `[DisableCors]` turns CORS off for one action inside a controller that has it.

</div>

## Reading origins from configuration

```json [appsettings.Development.json]
{
  "Cors": {
    "Origins": [ "http://localhost:4200" ]
  }
}
```

```csharp [Program.cs]
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
});
```

Keeping origins in [configuration](/web-api/configuration) lets you allow your local dev server in Development and your real domain in Production without changing code.

::: warning
`AllowAnyOrigin()` lets any website call your API from a visitor's browser. Avoid it for APIs that use cookies or return private data. ASP.NET Core also refuses to combine `AllowAnyOrigin()` with `AllowCredentials()`, because that would let any site act as the signed-in user.
:::

## Learn more

- [Enable Cross-Origin Requests (CORS) in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/cors)
