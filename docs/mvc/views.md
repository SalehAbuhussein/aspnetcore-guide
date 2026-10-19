# Views

## Introduction

A view is a `.cshtml` file that produces the HTML for a page. Views are written in Razor, a template syntax that lets you mix C# into HTML with the `@` character.

## Your first view

Create `Views/Books/Index.cshtml` for the `Index` action from the [Controllers](/mvc/controllers) page:

```razor [Views/Books/Index.cshtml]
@model List<Book>

@{
    ViewData["Title"] = "Books";
}

<h1>Books</h1>

@if (Model.Count == 0)
{
    <p>No books yet.</p>
}
else
{
    <ul>
        @foreach (var book in Model)
        {
            <li>
                <a asp-action="Details" asp-route-id="@book.Id">@book.Title</a>
                by @book.Author (@book.Year)
            </li>
        }
    </ul>
}
```

Visit `/Books` and you see the three books from the in-memory list. The controller called `View(_books)`, and the view receives that list as `Model`.

## Razor syntax

Razor switches between HTML and C# based on the `@` character.

| Syntax | Meaning |
| --- | --- |
| `@book.Title` | Write a C# value into the HTML |
| `@(book.Year + 1)` | Write the result of an expression |
| `@{ ... }` | Run a block of C# that writes nothing |
| `@if`, `@foreach`, `@for` | C# control flow, with HTML inside the braces |
| `@* note *@` | A comment that never reaches the browser |

Inside an `@if` or `@foreach` block, a line that starts with an HTML tag is treated as HTML. To write an email address or other text that contains `@`, Razor is smart enough to leave `name@example.com` alone. To output a literal `@` elsewhere, write `@@`.

## Strongly typed models

```razor
@model Book
```

The `@model` directive declares the type of data the view expects. A directive is a Razor keyword that configures the view rather than producing output. With it in place, `Model` has that type, your editor offers completion, and typos fail at build time.

Here is the details view:

```razor [Views/Books/Details.cshtml]
@model Book

@{
    ViewData["Title"] = Model.Title;
}

<h1>@Model.Title</h1>
<p>By @Model.Author, published in @Model.Year.</p>

<a asp-action="Index">Back to all books</a>
```

Note the lowercase `@model` declares the type, and the uppercase `Model` reads the value.

## ViewData compared to a model

`ViewData` is a dictionary of extra values. The views above set `ViewData["Title"]`, and the layout reads it to fill the `<title>` tag. That is a good use: a small value that many pages share.

```razor
<h2>@ViewData["Title"]</h2>
```

`ViewData` values are typed as `object`, and keys are plain strings. Nothing checks them at build time. Use a strongly typed model for the main data of a page, and keep `ViewData` for small extras. When a page needs several pieces of data, build a view model. See [Models](/mvc/models).

## HTML encoding

Razor encodes every value you write with `@`. If a book title is `<script>alert(1)</script>`, the browser shows the text instead of running it.

```razor
<p>@Model.Title</p>
@* Renders &lt;script&gt;alert(1)&lt;/script&gt; *@
```

This protects you from cross-site scripting (XSS), an attack where a user injects code into a page that other users see.

::: warning
`@Html.Raw(value)` turns encoding off. Never pass it text that came from a user.
:::

## _ViewImports.cshtml

```razor [Views/_ViewImports.cshtml]
@using BookShelf
@using BookShelf.Models
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

This file applies to every view in its folder and below. The `@using` lines are why the views above can write `Book` instead of `BookShelf.Models.Book`. `@addTagHelper` turns on the built-in tag helpers, such as `asp-action`. When you add a folder of classes your views need, add a `@using` line here.

## Learn more

- [Views in ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/mvc/views/overview)
- [Razor syntax reference for ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/views/razor)
