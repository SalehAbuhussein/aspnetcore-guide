# Layouts and partials

## Introduction

Most pages in an app share the same header, navigation, and footer. A layout holds that shared shell once. Partial views and view components let you reuse smaller pieces inside pages.

## The layout file

The template creates `Views/Shared/_Layout.cshtml`. Here is a trimmed version:

```razor [Views/Shared/_Layout.cshtml]
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - BookShelf</title>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
<body>
    <nav class="navbar navbar-expand bg-light mb-3">
        <div class="container">
            <a class="navbar-brand" asp-controller="Home" asp-action="Index">BookShelf</a>
            <a class="nav-link" asp-controller="Books" asp-action="Index">Books</a>
        </div>
    </nav>

    <main class="container">
        @RenderBody()
    </main>

    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

`@RenderBody()` marks where each page's own content goes. When `/Books` renders, the HTML from `Views/Books/Index.cshtml` is placed there. The `<title>` reads `ViewData["Title"]`, which each view sets.

## Sections

A section is a named block that a view can send to a specific spot in the layout. The layout above defines a `Scripts` section near the end of `<body>`. A view fills it like this:

```razor [Views/Books/Create.cshtml]
<h1>Add a book</h1>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

With `required: false`, views can skip the section. If you set it to `true`, every view must define it or the page throws an error. You can also write `@RenderSection("Scripts", required: false)`. The async version used by the template is preferred.

## _ViewStart.cshtml

```razor [Views/_ViewStart.cshtml]
@{
    Layout = "_Layout";
}
```

This file runs before every view in its folder and below. It sets the default layout, so your views do not need to name it. A single view can override it by setting `Layout` itself, or set `Layout = null;` to render with no layout.

## Partial views

A partial view is a view meant to be rendered inside other views. By convention its name starts with an underscore. Create a card for one book:

```razor [Views/Shared/_BookCard.cshtml]
@model Book

<div class="card mb-3">
    <div class="card-body">
        <h5 class="card-title">@Model.Title</h5>
        <p class="card-text">@Model.Author, @Model.Year</p>
        <a asp-controller="Books" asp-action="Details" asp-route-id="@Model.Id">Details</a>
    </div>
</div>
```

Then render it in a loop with the partial tag helper:

```razor [Views/Books/Index.cshtml]
@model List<Book>

<h1>Books</h1>

@foreach (var book in Model)
{
    <partial name="_BookCard" model="book" />
}
```

The `model` attribute passes data to the partial. MVC looks for the partial in the current controller's folder first, then in `Views/Shared/`. Partials receive data from their parent. They cannot load data on their own.

::: tip
Use a partial when you repeat the same markup in several places, such as a card, a table row, or a form block shared by Create and Edit.
:::

## View components

A view component is like a partial with its own C# class, so it can load its own data. Use one for widgets that appear on many pages, such as a "books on your shelf" counter in the navigation.

```csharp [ViewComponents/BookCountViewComponent.cs]
using Microsoft.AspNetCore.Mvc;

namespace BookShelf.ViewComponents;

public class BookCountViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(int count)
    {
        return View(count);
    }
}
```

```razor [Views/Shared/Components/BookCount/Default.cshtml]
@model int
<span class="badge bg-secondary">@Model books</span>
```

Render it from any view or the layout:

```razor
@await Component.InvokeAsync("BookCount", new { count = 3 })
```

The class name ends in `ViewComponent`, and you refer to it without that suffix. Its view lives in `Components/{Name}/Default.cshtml`. In a real app, the component would get the count from `AppDbContext` through its constructor. See [Dependency injection](/mvc/dependency-injection).

## Learn more

- [Layout in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/views/layout)
- [Partial views in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/views/partial)
