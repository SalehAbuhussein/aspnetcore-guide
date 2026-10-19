# Introduction

## Introduction

This guide teaches you how to build a Web API with ASP.NET Core and .NET 10. You will start from an empty project and finish with a working API that stores todo items in a database.

## What is a Web API?

A Web API is a program that answers HTTP requests with data instead of web pages. A mobile app, a JavaScript front end, or another server sends a request, and your API sends back JSON.

JSON is a plain text format for data. It looks like this:

```json
{
  "id": 1,
  "title": "Buy milk",
  "isDone": false
}
```

HTTP is the protocol browsers and apps use to talk to servers. Every request has a method, such as `GET` to read data or `POST` to create it, and a URL that says which data you want.

## How a request flows

When a client calls your API, three things happen in order:

1. The client sends a request to a URL, such as `GET https://localhost:5001/todos/1`.
2. ASP.NET Core finds the endpoint that matches that method and URL. An endpoint is a piece of code you write to handle one kind of request.
3. Your endpoint returns a C# object, and ASP.NET Core turns it into JSON and sends it back.

```csharp [Program.cs]
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/hello", () => new { Message = "Hello, world" });

app.Run();
```

A request to `/hello` returns `{"message":"Hello, world"}`. You never write JSON by hand. ASP.NET Core converts your objects for you.

## Minimal APIs or controllers

ASP.NET Core gives you two ways to write endpoints.

Minimal APIs define each endpoint with a method call and a lambda, which is a small inline function. They need very little code, start fast, and are the default in new .NET 10 projects.

Controllers group related endpoints into a class. Each public method in the class handles one request. This style is older and you will find it in many existing projects.

Both styles run on the same framework and can share models, services, and the database. This guide leads with Minimal APIs because they are shorter and easier to follow when you are new. The [Controllers](/web-api/controllers) page shows the same endpoints written as a controller, so you can read either style.

::: tip
If you are not sure which to pick, use Minimal APIs. You can add controllers to the same project later.
:::

## When to use MVC instead

A Web API returns data. If you want the server to build full HTML pages with forms and layouts, use ASP.NET Core MVC instead. The [MVC introduction](/mvc/introduction) explains that path.

## What you will build

Across these pages you will build `TodoApi`, a small API for a todo list. It uses one model:

```csharp [Models/Todo.cs]
namespace TodoApi.Models;

public class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
}
```

The API exposes endpoints under `/todos` to list, read, create, update, and delete todo items. The early pages keep the data in an in-memory list. The [Database](/web-api/database) page replaces that list with a real database.

Start with [Installation](/web-api/installation).

## Learn more

- [Choose between controller-based APIs and Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/apis)
- [Tutorial: Create a Minimal API with ASP.NET Core](https://learn.microsoft.com/aspnet/core/tutorials/min-web-api)
