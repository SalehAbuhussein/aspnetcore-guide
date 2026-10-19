# HTTP basics

## Introduction

A web API is a program that answers HTTP requests with data instead of web pages. This page explains the parts of HTTP you will meet in the rest of the guide, so terms such as "PUT", "header", and "404" make sense when they show up.

## Requests and responses

A client, such as a browser, a phone app, or curl, sends a request. The server sends back exactly one response. Here is a real pair from TodoApi:

```http
GET /todos?pageSize=2 HTTP/1.1
Host: localhost:5000
User-Agent: curl/8.15.0
Accept: */*

HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Mon, 19 Oct 2026 19:03:25 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"items":[{"id":1,"title":"Buy milk","isDone":false},{"id":2,"title":"Walk the dog","isDone":true}],"page":1,"pageSize":2,"totalCount":4,"totalPages":2}
```

The first line of the request names a method, a path, and the protocol version. The first line of the response holds a status code and a short reason. Headers follow, one per line. After a blank line comes the body, here a page of todos. `Kestrel` is the web server built into ASP.NET Core.

## Parts of a URL

```text
http://localhost:5000/todos/5?include=project
```

- The scheme is `http` or `https`. It says how to talk to the server.
- The host is `localhost`, the machine name. On a live server it is a domain such as `api.example.com`.
- The port is `5000`. One machine can run many servers, and the port picks one. `launchSettings.json` sets it for your app.
- The path is `/todos/5`. [Routing](/web-api/routing) matches it to one of your endpoints.
- The query string is `?include=project`, a list of `name=value` pairs after the `?`. APIs use it for filters, sorting, and [pagination](/web-api/pagination).

## Methods

The method says what the client wants to do with the thing at that path.

| Method | Meaning | TodoApi example |
| --- | --- | --- |
| `GET` | Read | `GET /todos/5` |
| `POST` | Create | `POST /todos` with a new todo in the body |
| `PUT` | Replace | `PUT /todos/5` with the whole todo |
| `PATCH` | Change part | `PATCH /todos/5` with only `isDone` |
| `DELETE` | Remove | `DELETE /todos/5` |

A safe method only reads and never changes data. `GET` is safe. An idempotent method gives the same result whether you send it once or five times. `GET`, `PUT`, and `DELETE` are idempotent: deleting todo 5 twice still leaves it deleted. `POST` is neither, so sending it twice creates two todos. Clients may retry idempotent requests after a network error, so keep your `PUT` and `DELETE` endpoints that way. The [CRUD](/web-api/crud) page builds one endpoint per method.

## Headers

Headers carry extra information about a request or response.

- `Content-Type` says what format the body is in. For JSON it is `application/json`.
- `Accept` says which formats the client would like back.
- `Authorization` carries credentials, such as `Bearer <token>`. See [Authentication](/web-api/authentication).
- `Location` points to a resource the server has created, or to where a redirect should go.
- `Cookie` and `Set-Cookie` store small values in a browser. Most APIs use tokens instead.

## Request and response bodies

`GET` and `DELETE` requests usually have no body. `POST`, `PUT`, and `PATCH` send one, and ASP.NET Core turns the JSON into a C# object for you. See [Reading requests](/web-api/requests). Here is a real create call:

```http
POST /projects HTTP/1.1
Host: localhost:5000
Content-Type: application/json

{"name":"Home"}

HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8
Date: Mon, 19 Oct 2026 19:03:25 GMT
Server: Kestrel
Location: /projects/2
Transfer-Encoding: chunked

{"id":2,"name":"Home","todoCount":0}
```

The `201` and the `Location` header tell the client where the new project lives.

## Status codes

The first digit tells you the family. Choosing the right code is part of your API's contract, covered on [Responses](/web-api/responses).

| Family | Meaning | Common codes |
| --- | --- | --- |
| 2xx | Success | `200 OK`, `201 Created`, `204 No Content` |
| 3xx | Go somewhere else | `301 Moved Permanently`, `302 Found` |
| 4xx | The client sent something wrong | `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict` |
| 5xx | The server failed | `500 Internal Server Error` |

`401` means "I do not know who you are". `403` means "I know who you are, and you may not do this". For errors, ASP.NET Core sends a JSON body in a standard shape called ProblemDetails:

```http
HTTP/1.1 404 Not Found
Content-Type: application/problem+json

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"traceId":"00-77da0b0e7c312a902822d97a89156b7a-0376e3bfec7e1d42-00"}
```

The [Error handling](/web-api/errors) page shows how to shape these.

## JSON

JSON is the text format almost every API uses for data. Objects go in `{}`, lists in `[]`, and values are strings, numbers, `true`, `false`, or `null`. ASP.NET Core writes C# property names in camelCase, so `IsDone` becomes `isDone`.

## HTTPS

HTTPS is HTTP sent through an encrypted connection. Nobody between the client and the server can read or change the traffic, which matters for tokens and passwords. ASP.NET Core trusts a development certificate on your machine, and `UseHttpsRedirection` in `Program.cs` sends plain HTTP calls to the HTTPS address. Always use HTTPS in production.

## Seeing it yourself

From a terminal, `curl -i` prints the status and headers above the body. Add `-v` to see the request headers too.

```bash
curl -i "http://localhost:5000/todos?pageSize=2"
curl -i -X POST http://localhost:5000/projects -H "Content-Type: application/json" -d "{\"name\":\"Home\"}"
```

The template also creates `TodoApi.http`. In VS Code with the REST Client extension, or in Visual Studio, click Send Request above any request to see the raw response next to the file. When a browser app calls your API, open its developer tools with F12 and pick the Network tab.

## Learn more

- [Routing](/web-api/routing)
- [Reading requests](/web-api/requests)
- [Responses](/web-api/responses)
- [Error handling](/web-api/errors)
- [ASP.NET Core fundamentals overview](https://learn.microsoft.com/aspnet/core/fundamentals/)
