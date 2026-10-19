# Cookie auth for SPAs

## Introduction

A single page app (SPA), such as the Angular app from [Calling from Angular](/web-api/angular-client), runs in the browser and needs to sign users in. When the SPA and the API are served from the same site, the simplest safe choice is a cookie. This page signs in with the Identity API endpoints in cookie mode, calls protected endpoints with `fetch`, and explains why this beats keeping tokens in `localStorage`.

You need Identity set up as shown in [Identity API endpoints](/web-api/authentication#identity-api-endpoints): the package, `AppDbContext` inheriting from `IdentityDbContext<IdentityUser>`, and the migration applied.

<ApiStyle />

## Registering Identity

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.Identity;

// before builder.Build()
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<AppDbContext>();

// after builder.Build()
app.MapIdentityApi<IdentityUser>();
```

`AddIdentityApiEndpoints` registers both a cookie scheme and a bearer token scheme. `MapIdentityApi` adds `/register`, `/login`, `/refresh`, and a few account endpoints. The same `/login` endpoint can answer with a cookie or with tokens, depending on a query string flag.

## Signing in with a cookie

```http
POST /login?useCookies=true
Content-Type: application/json

{ "email": "ada@example.com", "password": "Passw0rd!" }
```

With `useCookies=true`, a successful login returns `200 OK` with an empty body and a `Set-Cookie` header named `.AspNetCore.Identity.Application`. The cookie is `HttpOnly` and `SameSite=Lax` by default, and it lasts 14 days. The browser stores it and sends it back on every later request to the same site, so your JavaScript never touches it. Add `useSessionCookies=true` instead if the cookie should disappear when the browser closes.

## Who am I, and signing out

`MapIdentityApi` has no logout endpoint, so add one next to a small "who am I" endpoint.

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
app.MapGet("/me", (HttpContext context) => context.User.Identity?.Name)
    .RequireAuthorization();

app.MapPost("/logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return TypedResults.Ok();
}).RequireAuthorization();
```

</div>

<div class="only-controllers">

```csharp [Controllers/AccountController.cs]
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Controllers;

[ApiController]
[Authorize]
public class AccountController(SignInManager<IdentityUser> signInManager) : ControllerBase
{
    [HttpGet("me")]
    public string? Me() => User.Identity?.Name;

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok();
    }
}
```

</div>

`SignOutAsync` tells the browser to delete the cookie. When a request has no valid cookie, these endpoints answer `401 Unauthorized`. Identity API endpoints return 401 instead of redirecting to a login page, which is what a SPA wants.

## Calling the API from the browser

```ts [src/auth.ts]
export async function login(email: string, password: string): Promise<boolean> {
  const res = await fetch('/login?useCookies=true', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
    credentials: 'include',
  })
  return res.ok
}

export async function logout(): Promise<void> {
  await fetch('/logout', { method: 'POST', credentials: 'include' })
}
```

`credentials: 'include'` tells `fetch` to send and accept cookies. For same-origin requests, the default `'same-origin'` already does that, so the option matters when the API is on another origin. Add it to every call that needs the user, such as `fetch('/me', { credentials: 'include' })`. In development, add `/login`, `/logout`, and `/me` to `proxy.conf.json` from [Calling from Angular](/web-api/angular-client) so the cookie is set on the dev server's origin.

## Why cross-site forgery is not a problem here

Cross-site request forgery (CSRF) is when another website makes the visitor's browser send a request to your API, and the browser attaches your cookie. Two things block that here:

- `SameSite=Lax` tells the browser not to send the cookie on POST, PUT, or DELETE requests that start on a different site. Only top-level GET navigations from other sites carry it, and GET endpoints should never change data.
- Your endpoints read JSON. A page on another site cannot send a JSON body with `Content-Type: application/json` without a CORS preflight, and your API does not allow that origin.

Endpoints that read HTML form posts are different, because a plain form from any site can post to them. Protect those with an antiforgery token: Minimal APIs check it for `[FromForm]` parameters once you call `AddAntiforgery` and `UseAntiforgery`, and controllers use `[ValidateAntiForgeryToken]`.

## Why cookies beat tokens in localStorage

Any script running on your page can read `localStorage`. If an attacker gets one script onto your page, through a bad npm package or a cross-site scripting bug, they can copy the token and use it from their own machine until it expires. An `HttpOnly` cookie cannot be read by JavaScript at all, so the same bug cannot steal it. The attacker can still act inside the open tab, but loses access once the user closes it. Cookies also need no client code to attach, store, or refresh tokens.

Use bearer tokens for mobile apps, desktop apps, and other servers, where there is no browser cookie jar. See [Refresh tokens](/web-api/refresh-tokens).

## When the SPA is on another site

If the SPA lives on a different site than the API, such as `app.example.com` calling `api.other.com`, the browser treats the cookie as third-party and many browsers block it. The usual answer is the backend for frontend (BFF) pattern. You add a small server on the SPA's own site that serves the SPA, signs the user in with a cookie, and forwards API calls to the real API with a token it keeps on the server. The browser only ever sees a same-site cookie, and tokens never reach JavaScript. YARP, Microsoft's reverse proxy library, is a common way to build the forwarding part.

## Trying it with curl

```bash
curl -c cookies.txt "https://localhost:7287/login?useCookies=true" \
  -H "Content-Type: application/json" \
  -d '{"email":"ada@example.com","password":"Passw0rd!"}'

curl -b cookies.txt https://localhost:7287/me
```

Register the user first with `POST /register` and the same JSON body. `-c` saves the cookie to a file and `-b` sends it back. The second command prints the email address. Without `-b`, it returns `401`. Add `-k` if curl does not trust the development certificate.

## Learn more

- [Authentication](/web-api/authentication)
- [Refresh tokens](/web-api/refresh-tokens)
- [CORS](/web-api/cors)
- [Use Identity to secure a Web API backend for SPAs](https://learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization)
