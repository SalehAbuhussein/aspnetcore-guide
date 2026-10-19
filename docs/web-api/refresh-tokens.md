# Refresh tokens

## Introduction

Access tokens should be short-lived, so a stolen one stops working soon. Users should not have to type their password every 15 minutes, though. A refresh token solves this: a long-lived token the client trades for a new access token. This page uses the refresh tokens that the Identity API endpoints already issue, shows the client logic in TypeScript, and sketches a refresh token table for when you issue your own JWTs.

You need Identity set up as shown in [Identity API endpoints](/web-api/authentication#identity-api-endpoints). For browser apps on the same site as the API, prefer [Cookie auth for SPAs](/web-api/spa-auth), which needs no refresh logic in JavaScript.

## Logging in for tokens

```http
POST /login
Content-Type: application/json

{ "email": "ada@example.com", "password": "Passw0rd!" }
```

Without `useCookies=true`, `/login` answers with tokens:

```json
{
  "tokenType": "Bearer",
  "accessToken": "CfDJ8Kc68gsa3vJH...",
  "expiresIn": 3600,
  "refreshToken": "CfDJ8Kc68gsa3vJH..."
}
```

`accessToken` goes in the `Authorization: Bearer <token>` header of each request. `expiresIn` is its lifetime in seconds. `refreshToken` is only for getting new tokens. These are not JWTs. Identity encrypts them with ASP.NET Core Data Protection, so only your API can read them.

## Refreshing

```http
POST /refresh
Content-Type: application/json

{ "refreshToken": "CfDJ8Kc68gsa3vJH..." }
```

A valid refresh token returns `200 OK` with the same JSON shape as `/login`, including a new refresh token. Store the new one and drop the old one. An expired or invalid refresh token returns `401`, and the user has to log in again. `/refresh` also checks that the user still exists and that their security stamp has not changed, so changing a password ends old sessions.

## Configuring lifetimes

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;

builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
{
    options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
    options.RefreshTokenExpiration = TimeSpan.FromDays(7);
});
```

The defaults are one hour for access tokens and 14 days for refresh tokens. `BearerTokenOptions` is a named options type, so you pass `IdentityConstants.BearerScheme` to configure the scheme that Identity registered. With this code, `/login` returns `"expiresIn": 900`.

## Refreshing on 401 in the client

```ts [src/apiClient.ts]
interface AccessTokenResponse {
  tokenType: string
  accessToken: string
  expiresIn: number
  refreshToken: string
}

let accessToken: string | null = null
let refreshToken: string | null = null

function store(tokens: AccessTokenResponse) {
  accessToken = tokens.accessToken
  refreshToken = tokens.refreshToken
}

export async function login(email: string, password: string): Promise<boolean> {
  const res = await fetch('/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) return false
  store((await res.json()) as AccessTokenResponse)
  return true
}

let refreshing: Promise<boolean> | null = null

function refresh(): Promise<boolean> {
  if (!refreshToken) return Promise.resolve(false)
  // Share one refresh between requests that fail at the same time.
  refreshing ??= fetch('/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  })
    .then(async (res) => {
      if (!res.ok) {
        accessToken = refreshToken = null
        return false
      }
      store((await res.json()) as AccessTokenResponse)
      return true
    })
    .finally(() => {
      refreshing = null
    })
  return refreshing
}

function withToken(init: RequestInit): RequestInit {
  const headers = new Headers(init.headers)
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  return { ...init, headers }
}

export async function apiFetch(url: string, init: RequestInit = {}): Promise<Response> {
  const res = await fetch(url, withToken(init))
  if (res.status !== 401) return res

  const refreshed = await refresh()
  if (!refreshed) return res // send the user to the login screen

  return fetch(url, withToken(init))
}
```

`apiFetch` sends the request with the current access token. On a `401`, it refreshes once and retries once. If the retry also fails, it returns that response, so a bad request can never loop. When several requests fail together, they all wait on the same `refreshing` promise, so only one `/refresh` call goes out. The tokens live in memory, which means a page reload logs the user out. Keeping the refresh token in `localStorage` survives reloads but lets any script on the page steal it, which is the reason [cookies](/web-api/spa-auth) are the better fit for browsers.

## When you issue your own JWTs

If you use your own token service with `AddJwtBearer` instead of Identity, you build refresh tokens yourself. A common design is one table:

| Column | Purpose |
| --- | --- |
| `Id` | Primary key |
| `UserId` | Who the token belongs to |
| `TokenHash` | SHA-256 hash of the token, never the token itself |
| `ExpiresAt` | When it stops working |
| `CreatedAt`, `RevokedAt` | When it was issued and when it was used or revoked |
| `ReplacedByTokenId` | The token that replaced it, to follow a chain |

Generate the token with `RandomNumberGenerator.GetBytes(32)` and send it to the client once. Store only its hash, like a password, so a database leak does not hand out working tokens. On each refresh, look up the hash, check it is not expired or revoked, mark it revoked, and issue a new one. This is called rotation. If a token that was already revoked comes back, someone copied it, so revoke every token in that chain and make the user log in again. Delete expired rows with a [background task](/web-api/background-tasks).

## Trying it with curl

```bash
curl -X POST https://localhost:7287/register -H "Content-Type: application/json" \
  -d '{"email":"ada@example.com","password":"Passw0rd!"}'

curl -X POST https://localhost:7287/login -H "Content-Type: application/json" \
  -d '{"email":"ada@example.com","password":"Passw0rd!"}'

curl -X POST https://localhost:7287/refresh -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken from the login response>"}'
```

The login and refresh calls both print a JSON object with `accessToken`, `expiresIn`, and `refreshToken`. Sending a made-up refresh token returns `401`.

## Learn more

- [Authentication](/web-api/authentication)
- [Cookie auth for SPAs](/web-api/spa-auth)
- [Use Identity to secure a Web API backend for SPAs](https://learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization)
