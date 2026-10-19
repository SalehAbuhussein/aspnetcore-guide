# Introduction

## Introduction

ASP.NET Core MVC is a framework for building web apps that return HTML pages. This track teaches it from an empty project to a deployed app. Along the way you build BookShelf, a small app for tracking the books you own.

## What MVC means

MVC stands for Model, View, Controller. It is a way to split a web app into three kinds of code, each with one job.

- A model is a C# class that holds data, such as a `Book` with a title and an author.
- A view is a template file that turns a model into HTML.
- A controller is a C# class that receives a request, gets the data it needs, and picks a view to render.

Keeping these apart means you can change how a page looks without touching the code that loads the data, and the other way around.

## How a request flows

```text
Browser asks for https://localhost:5001/Books/Details/3
        |
        v
Routing matches the URL to BooksController.Details(id: 3)
        |
        v
The controller action loads Book #3 (the model)
        |
        v
The action returns View(book)
        |
        v
Razor renders Views/Books/Details.cshtml into HTML
        |
        v
The HTML goes back to the browser
```

Every page in an MVC app follows this path. Routing is the part of ASP.NET Core that reads the URL and decides which code should run. An action is a public method on a controller that handles one kind of request. Razor is the template language views are written in. It mixes HTML with C#.

Each step has its own page in this track: [Routing](/mvc/routing), [Controllers](/mvc/controllers), [Models](/mvc/models), and [Views](/mvc/views).

## MVC or Web API

ASP.NET Core gives you two common ways to build server apps. Pick the one that matches what your app sends back.

| You want to | Use |
| --- | --- |
| Render full HTML pages on the server | MVC |
| Return JSON to a JavaScript front end or mobile app | [Web API](/web-api/introduction) |
| Do both in one project | MVC, with some API controllers added |

MVC and Web API share the same routing, controllers, and model binding. What you learn here carries over. The main difference is that an MVC action usually returns a view, while an API action returns data.

## What you will build

BookShelf is a web app for managing a list of books. By the end of the track it can:

- list books and show details for one book
- add, edit, and delete books through HTML forms
- validate input and show error messages
- store books in a real database with Entity Framework Core
- let users sign in and restrict who can make changes

Every page uses the same `Book` model:

```csharp [Models/Book.cs]
namespace BookShelf.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int Year { get; set; }
}
```

In the early pages, books live in a list in memory. They disappear when the app restarts. The [Database](/mvc/database) page replaces that list with a real database.

::: tip
You do not need to know C# well to follow along. If you have written code in another language, the syntax will feel familiar. Each new term gets a one-line explanation the first time it appears.
:::

## Learn more

- [Overview of ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/mvc/overview)
- [Get started with ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/tutorials/first-mvc-app/start-mvc)
