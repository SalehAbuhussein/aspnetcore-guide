# HTTP basics

## Introduction

Every page in BookShelf travels over HTTP, the plain-text conversation between a browser and a server. This page explains the parts of that conversation you will meet in the rest of the guide, so terms such as "POST", "redirect", and "302" make sense when they show up.

## Requests and responses

The browser sends a request. The server sends back exactly one response. Here is a real pair from BookShelf:

```http
GET /Books HTTP/1.1
Host: localhost:5261
Accept: text/html

HTTP/1.1 200 OK
Content-Type: text/html; charset=utf-8
Date: Mon, 19 Oct 2026 19:03:30 GMT
Server: Kestrel
Transfer-Encoding: chunked

<!DOCTYPE html>
<html lang="en">
...
```

The first line of the request names a method, a path, and the protocol version. The first line of the response holds a status code and a short reason. Headers follow, one per line. After a blank line comes the body, here the HTML for the book list. `Kestrel` is the web server built into ASP.NET Core.

## Parts of a URL

```text
http://localhost:5261/Books/Details/5?tab=reviews
```

- The scheme is `http` or `https`. It says how to talk to the server.
- The host is `localhost`, the machine name. On a live site it is a domain such as `example.com`.
- The port is `5261`. One machine can run many servers, and the port picks one. `launchSettings.json` sets it for your app.
- The path is `/Books/Details/5`. [Routing](/mvc/routing) turns it into a call to the `Details` action on `BooksController`.
- The query string is `?tab=reviews`, a list of `name=value` pairs after the `?`.

## Methods

The method says what the browser wants to do.

| Method | Meaning |
| --- | --- |
| `GET` | Read something, such as a page. |
| `POST` | Send data, such as a new book from a form. |
| `PUT` | Replace something. |
| `PATCH` | Change part of something. |
| `DELETE` | Remove something. |

A safe method only reads and never changes data. `GET` is safe, so browsers and search engines feel free to repeat it. An idempotent method gives the same result whether you send it once or five times. `GET`, `PUT`, and `DELETE` are idempotent. `POST` is neither, which is why a browser asks before it resends a form.

HTML forms can only send `GET` and `POST`. That is why MVC apps use `POST` for create, edit, and delete. See [Forms](/mvc/forms).

## Headers

Headers carry extra information about a request or response.

- `Content-Type` says what format the body is in, such as `text/html` or `application/x-www-form-urlencoded` for a submitted form.
- `Accept` says which formats the browser would like back.
- `Cookie` sends cookies from the browser to the server. `Set-Cookie` asks the browser to store one.
- `Location` tells the browser where to go next after a redirect.
- `Authorization` carries credentials. Browser apps like BookShelf usually use a login cookie instead.

## Cookies

HTTP has no memory. Each request stands alone. A cookie is a small value the server asks the browser to store and send back on every later request. BookShelf uses cookies for the login, for [sessions](/mvc/sessions), and for the antiforgery token that protects forms:

```http
Set-Cookie: .AspNetCore.Antiforgery.8c90XH5aC5E=CfDJ8Kc6...; path=/; samesite=strict; httponly
```

`httponly` stops JavaScript from reading the cookie. You set up the login cookie on the [Authentication](/mvc/authentication) page.

## Redirects

A redirect is a response that says "go to this other URL instead". Ask for a page that needs a login and BookShelf answers:

```http
HTTP/1.1 302 Found
Content-Length: 0
Location: http://localhost:5261/Identity/Account/Login?ReturnUrl=%2FBooks%2FCreate
```

The browser follows `Location` on its own. MVC apps also redirect after every successful form post, a pattern called Post/Redirect/Get. It stops a refresh from submitting the form twice. `RedirectToAction` in a controller sends this kind of response.

## Status codes

The first digit tells you the family.

| Family | Meaning | Common codes |
| --- | --- | --- |
| 2xx | Success | `200 OK` |
| 3xx | Go somewhere else | `301 Moved Permanently`, `302 Found` |
| 4xx | The request was wrong | `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found` |
| 5xx | The server failed | `500 Internal Server Error` |

A 4xx code means the browser asked for something it cannot have. A 5xx code means your code threw an error. The [Error handling](/mvc/errors) page shows friendly pages for both.

## JSON

JSON is a text format for data, such as `{"title":"Dune","year":1965}`. BookShelf mostly sends HTML, but you will see JSON when a page calls the server from JavaScript, for example with [htmx](/mvc/htmx), and when you call other APIs with [HTTP clients](/mvc/http-clients).

## HTTPS

HTTPS is HTTP sent through an encrypted connection. Nobody between the browser and the server can read or change the traffic, which matters for passwords and cookies. ASP.NET Core trusts a development certificate on your machine, and `UseHttpsRedirection` in `Program.cs` sends plain HTTP visitors to the HTTPS address.

## Seeing it yourself

Open your browser's developer tools with F12 and pick the Network tab. Reload the page and click any row to see its method, status, request headers, and response headers. Tick Preserve log to keep requests across redirects.

From a terminal, `curl -i` prints the response status and headers above the body:

```bash
curl -i http://localhost:5261/Books
```

Add `-v` to see the request headers that curl sent too.

## Learn more

- [Routing](/mvc/routing)
- [Controllers](/mvc/controllers)
- [Forms](/mvc/forms)
- [ASP.NET Core fundamentals overview](https://learn.microsoft.com/aspnet/core/fundamentals/)
