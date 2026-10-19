# Routing

## Introduction

Routing decides which controller action runs for a given URL. ASP.NET Core MVC supports two styles. Conventional routing uses one URL pattern for the whole app, and attribute routing puts the URL on each action.

## The default route

```csharp [Program.cs]
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
```

The template adds this conventional route. The parts in braces are route parameters, placeholders that capture part of the URL. The pattern has three:

- `{controller=Home}` picks the controller. It defaults to `Home`.
- `{action=Index}` picks the method on that controller. It defaults to `Index`.
- `{id?}` is optional, because of the `?`. If present, it is passed to the action's `id` parameter.

Here is how some URLs match:

| URL | Runs |
| --- | --- |
| `/` | `HomeController.Index()` |
| `/Books` | `BooksController.Index()` |
| `/Books/Details/3` | `BooksController.Details(3)` |
| `/Books/Create` | `BooksController.Create()` |

Matching is not case sensitive, so `/books/details/3` works too. With this one route, every new controller and action you add gets a URL without extra setup.

## Attribute routing

```csharp [Controllers/BooksController.cs]
using Microsoft.AspNetCore.Mvc;

namespace BookShelf.Controllers;

public class BooksController : Controller
{
    [HttpGet("books/{id:int}")]
    public IActionResult Details(int id)
    {
        return Content($"Book {id}");
    }
}
```

An attribute is a tag in square brackets that adds information to a class or method. `[HttpGet("books/{id:int}")]` says this action handles GET requests to `/books/3`. Once an action has a route attribute, the conventional route no longer reaches it. `/Books/Details/3` returns 404.

To share a prefix across actions, put `[Route]` on the controller:

```csharp [Controllers/BooksController.cs]
[Route("books")]
public class BooksController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => Content("All books");

    [HttpGet("{id:int}")]
    public IActionResult Details(int id) => Content($"Book {id}");

    [HttpGet("new")]
    public IActionResult Create() => Content("New book form");
}
```

Now `/books`, `/books/3`, and `/books/new` each map to one action. The examples in the rest of this track use the conventional route to keep things short.

## Route constraints

A constraint limits what a route parameter can match. It goes after a colon inside the braces.

| Route template | Matches |
| --- | --- |
| `books/{id:int}` | `books/3`, not `books/abc` |
| `books/year/{year:int:min(1450)}` | a whole number of 1450 or more |
| `authors/{name:alpha}` | letters only |
| `orders/{orderId:guid}` | a GUID, such as `3f2504e0-4f89-11d3-9a0c-0305e82c3301` |

If the URL does not satisfy the constraint, the route does not match and the app returns 404. Constraints help tell routes apart, such as `books/3` and `books/new`. Do not use them for input validation. Use [Validation](/mvc/validation) for that.

::: tip
Keep constraints simple. `int`, `guid`, `alpha`, `min`, `max`, and `length` cover most needs.
:::

## Generate URLs

Do not type URLs by hand in views. Let routing build them, so they stay correct if your routes change.

In a view, use the anchor tag helper. A tag helper is a special HTML attribute that runs C# on the server when the page renders.

```razor [Views/Books/Index.cshtml]
<a asp-controller="Books" asp-action="Details" asp-route-id="3">Dune</a>
```

This renders `<a href="/Books/Details/3">Dune</a>` with the default route. `asp-route-id` fills the `id` parameter. Any `asp-route-{name}` attribute fills the parameter with that name. If you leave out `asp-controller`, the current controller is used.

In C# code, use `Url.Action`:

```csharp
string? url = Url.Action("Details", "Books", new { id = 3 });
// "/Books/Details/3"
```

The third argument is an anonymous object whose properties become route values. Both approaches respect attribute routes too. If `Details` has `[HttpGet("books/{id:int}")]`, they produce `/books/3`.

## Learn more

- [Routing to controller actions in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/controllers/routing)
- [Routing in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/routing)
