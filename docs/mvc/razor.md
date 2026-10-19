# Razor in depth

## Introduction

The [Views](/mvc/views) page showed the basics of Razor. This page fills in the rest: expressions, control flow, directives, display and editor templates, the built-in tag helpers you use outside forms, and a short look at Razor Pages.

## Implicit and explicit expressions

```razor [Views/Books/Details.cshtml (excerpt)]
<p>@Model.Title by @Model.Author</p>
<p>Published @(DateTime.Now.Year - Model.Year) years ago.</p>
<p>Code: @(Model.Id)A</p>
<p>Contact books@bookshelf.dev or follow @@bookshelf.</p>
```

An implicit expression starts with `@` and ends at the first space or character that cannot be part of a C# name. `@Model.Title` works, but `@DateTime.Now.Year - Model.Year` would print the year and then the text `- Model.Year`. Wrap anything with spaces, operators, or generics in `@( )` to make it explicit. Use `@( )` too when text follows the value with no space, as in `@(Model.Id)A`.

Razor treats `name@domain` as an email address and leaves it alone. Anywhere else, `@@` writes a single `@`.

## Code blocks and mixed text

```razor [Views/Books/Details.cshtml (excerpt)]
@{
    var age = DateTime.Now.Year - Model.Year;
    var label = age > 50 ? "classic" : "modern";
}

@if (age > 50)
{
    <text>This book is a classic.</text>
}
else
{
    @:This book is @age years old, a @label read.
}

@* Comments like this one never reach the browser. *@
```

`@{ }` runs C# and writes nothing. Variables declared there are available in the rest of the view.

Inside a code block, Razor needs to know when a line is text. A line that starts with an HTML tag is fine. For plain text with no tag around it, use `<text>...</text>` for several lines or `@:` for one line. The `<text>` tag itself is not sent to the browser.

`@* *@` is a Razor comment. An HTML comment `<!-- -->` is sent to the browser, so anyone can read it with View Source.

## Control flow

```razor [Views/Books/Index.cshtml (excerpt)]
@foreach (var (book, i) in Model.Books.Select((b, i) => (b, i)))
{
    <tr class="@(i % 2 == 0 ? "even" : "odd")">
        <td>@(i + 1)</td>
        <td>@book.Title</td>
        <td>
            @switch (book.ReviewCount)
            {
                case 0:
                    <span>No reviews</span>
                    break;
                case 1:
                    <span>1 review</span>
                    break;
                default:
                    <span>@book.ReviewCount reviews</span>
                    break;
            }
        </td>
    </tr>
}
```

`@if`, `@else`, `@switch`, and `@foreach` work like their C# versions, with HTML inside the braces. `foreach` has no index, so the example pairs each book with its position using `Select((b, i) => (b, i))`. When you need the index for most of the loop, `@for` reads better:

```razor [Views/Books/Index.cshtml (excerpt)]
@for (var i = 0; i < Model.Books.Count; i++)
{
    <p>@(i + 1). @Model.Books[i].Title</p>
}
```

`@while` and `@do` exist, but a loop that runs until some condition changes is logic, and logic belongs in the controller. `@try` and `@catch` exist too. Use them rarely. A view that catches its own errors hides bugs, and [error handling](/mvc/errors) for the whole app is a better place for them.

## @model and @using

```razor [Views/Books/Index.cshtml (excerpt)]
@using System.Globalization
@model BookListViewModel

<p>Updated @DateTime.Now.ToString("d", CultureInfo.InvariantCulture)</p>
```

`@model` sets the type of `Model`, as the [Views](/mvc/views) page explained. `@using` imports a namespace for one view. Put namespaces that many views share in `_ViewImports.cshtml` instead.

## @inject

```razor [Views/Shared/_Layout.cshtml (excerpt)]
@inject IWebHostEnvironment Env

<footer>Running in @Env.EnvironmentName</footer>
```

`@inject` asks the container for a service and gives it a name in the view. It works for any registered service. See [Dependency injection](/mvc/dependency-injection). Keep it to small display helpers. If a view injects `AppDbContext` to run queries, move that query to the controller or a [view component](/mvc/view-components).

## @functions

```razor [Views/Books/Details.cshtml (excerpt)]
@functions {
    string Stars(int rating) => new string('*', rating);
}

@foreach (var review in Model.Reviews)
{
    <p>@Stars(review.Rating) @review.Body</p>
}
```

`@functions` adds methods to the class Razor generates for the view. It is fine for tiny formatting helpers that one view uses. When a helper grows, is shared by several views, or touches data, move it to a C# class, a view model property, or the controller, where you can test it.

## @section

```razor [Views/Books/Details.cshtml (excerpt)]
@section Scripts {
    <script src="~/js/reviews.js" asp-append-version="true"></script>
}
```

A view fills a section the layout declares with `RenderSectionAsync("Scripts", required: false)`. [Layouts and partials](/mvc/layouts) covers how sections work. With `required: false`, a view can leave the section out and the page still renders.

## Async in views

```razor [Views/Books/Index.cshtml (excerpt)]
@await Html.PartialAsync("_BookCard", firstBook)
@await Component.InvokeAsync("RecentBooks")
```

Razor views can `await`, so use the async helpers: `Html.PartialAsync`, `Component.InvokeAsync`, and `RenderSectionAsync`. The `<partial>` tag helper is already async.

::: warning
Never call `.Result` or `.Wait()` on a task in a view, as in `Html.PartialAsync("_BookCard").Result`. It blocks a thread while the page renders and can deadlock under load. Write `@await` instead.
:::

## @addTagHelper and @removeTagHelper

```razor [Views/_ViewImports.cshtml (excerpt)]
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@removeTagHelper Microsoft.AspNetCore.Mvc.TagHelpers.CacheTagHelper, Microsoft.AspNetCore.Mvc.TagHelpers
```

`@addTagHelper` turns tag helpers on, as shown in [View components and tag helpers](/mvc/view-components). `@removeTagHelper` turns one off for the views in that folder. The first part is the full class name, the second the assembly. To skip tag helper processing on a single element, prefix the tag with `!`, as in `<!a href="/">`.

## Display names and data types

```csharp [Models/Book.cs (excerpt)]
using System.ComponentModel.DataAnnotations;

public class Book
{
    [Display(Name = "Year published")]
    public int Year { get; set; }
}
```

`[Display(Name)]` sets the label text that Razor shows for a property. `[DataType]`, such as `[DataType(DataType.Date)]` on a `DateTime`, tells Razor what kind of value it is, so templates can pick a better rendering. `DataType.Date` shows only the date, `DataType.EmailAddress` and `DataType.Url` render as links, and `DataType.MultilineText` gives a `<textarea>` in editor templates.

## DisplayFor, EditorFor, and DisplayNameFor

```razor [Views/Books/Details.cshtml (excerpt)]
<dl>
    <dt>@Html.DisplayNameFor(m => m.Title)</dt>
    <dd>@Html.DisplayFor(m => m.Title)</dd>
    <dt>@Html.DisplayNameFor(m => m.Year)</dt>
    <dd>@Html.DisplayFor(m => m.Year)</dd>
</dl>
```

`DisplayNameFor` writes the display name, so the page shows "Year published". `DisplayFor` writes the value with the right template for its type. `EditorFor` does the same for an input, as in `@Html.EditorFor(m => m.Year)`.

In a table, `DisplayNameFor` works on a list too. It reads the attribute without needing a row:

```razor [Views/Books/List.cshtml]
@model List<Book>

<table>
    <tr>
        <th>@Html.DisplayNameFor(m => m[0].Title)</th>
        <th>@Html.DisplayNameFor(m => m[0].Year)</th>
    </tr>
    @foreach (var book in Model)
    {
        <tr>
            <td>@Html.DisplayFor(_ => book.Title)</td>
            <td>@Html.DisplayFor(_ => book.Year)</td>
        </tr>
    }
</table>
```

The expression `m[0]` is never run, so an empty list is fine.

## Display and editor templates

```razor [Views/Shared/DisplayTemplates/Book.cshtml]
@model Book

<span class="book">
    <strong>@Model.Title</strong> by @Model.Author, @Model.Year
</span>
```

```razor [Views/Shared/EditorTemplates/DateTime.cshtml]
@model DateTime

<input type="date" name="@Html.NameForModel()" value="@Model.ToString("yyyy-MM-dd")" class="form-control" />
```

A template is a partial view named after a type. Put display templates in `DisplayTemplates/` and editor templates in `EditorTemplates/`, under `Views/Shared/` or a controller's view folder. Now `@Html.DisplayFor(m => m)` on a `Book` uses `Book.cshtml`, and `EditorFor` on any `DateTime` property uses `DateTime.cshtml`. `DisplayFor` on a `List<Book>` renders the `Book` template once for each item.

## Environment and cache tag helpers

```razor [Views/Shared/_Layout.cshtml (excerpt)]
<environment include="Development">
    <p class="text-bg-warning">Development build</p>
</environment>

<cache expires-after="@TimeSpan.FromMinutes(5)">
    <p>Shelf snapshot taken at @DateTime.Now.ToString("T")</p>
</cache>
```

`<environment>` renders its content only in the listed environments. Use `exclude` for the opposite. `<cache>` stores the rendered HTML in memory and reuses it until it expires. See [Caching](/mvc/caching) for other options.

## Cache busting with asp-append-version

```razor [Views/Shared/_Layout.cshtml (excerpt)]
<img src="~/images/logo.png" asp-append-version="true" alt="BookShelf" />
<script src="~/js/site.js" asp-append-version="true"></script>
```

`asp-append-version` adds a hash of the file to the URL, such as `site.js?v=Kl_dqr9N`. When the file changes, the URL changes, so browsers fetch the new version. See [Static files](/mvc/static-files).

## Select lists

```csharp [Controllers/BooksController.cs (excerpt)]
public async Task<IActionResult> ByAuthor(string? author)
{
    var authors = await db.Books.Select(b => b.Author).Distinct().OrderBy(a => a).ToListAsync();

    var books = await db.Books.Where(b => author == null || b.Author == author).ToListAsync();
    return View(new AuthorFilter { Author = author, Authors = new SelectList(authors), Books = books });
}
```

```csharp [Models/AuthorFilter.cs]
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BookShelf.Models;

public class AuthorFilter
{
    public string? Author { get; set; }
    public SelectList Authors { get; set; } = new(Array.Empty<string>());
    public List<Book> Books { get; set; } = [];
}
```

```razor [Views/Books/ByAuthor.cshtml]
@model AuthorFilter

<form method="get">
    <select asp-for="Author" asp-items="Model.Authors">
        <option value="">All authors</option>
    </select>
    <button type="submit">Filter</button>
</form>

@Html.DisplayFor(m => m.Books)
```

`SelectList` turns a list into `<option>` tags. `asp-for` sets the `name` and `id`, and marks the option that matches `Author` as selected. Options you write by hand, such as "All authors", stay at the top. Add `using Microsoft.AspNetCore.Mvc.Rendering;` to the controller for `SelectList`.

## Partial and anchor tag helpers

```razor [Views/Books/Index.cshtml (excerpt)]
<partial name="_BookCard" model="firstBook" />

<a asp-action="ByAuthor" asp-route-author="Andy Weir">Books by Andy Weir</a>
<a asp-controller="Books" asp-action="Details" asp-route-id="3">Book 3</a>
```

`<partial name>` is the tag helper form of `Html.PartialAsync`. On links, `asp-route-*` fills route values. A name that matches the route template, such as `id`, goes in the path as `/Books/Details/3`. Any other name goes in the query string as `/Books/ByAuthor?author=Andy%20Weir`. See [Routing](/mvc/routing).

## Raw HTML

```razor [Views/Home/Index.cshtml (excerpt)]
@{
    var notice = "<strong>Library closed</strong> on Friday.";
}
<p>@Html.Raw(notice)</p>
```

`@Html.Raw` writes a string without encoding it. If the string contains a script from a user, that script runs in every visitor's browser. This is cross-site scripting, as described in [Views](/mvc/views).

Use it only for HTML that your own code wrote, such as a constant string or markdown your app converted and sanitized. Never use it for titles, reviews, or anything a user typed.

## Razor Pages

```razor [Pages/Shelf.cshtml]
@page
@model BookShelf.Pages.ShelfModel

<h1>@Model.Count books on the shelf</h1>
```

```csharp [Pages/Shelf.cshtml.cs]
using BookShelf.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookShelf.Pages;

public class ShelfModel(AppDbContext db) : PageModel
{
    public int Count { get; private set; }

    public void OnGet() => Count = db.Books.Count();
}
```

Razor Pages is a second way to build pages in ASP.NET Core. Each page is a `.cshtml` file that starts with `@page`, paired with a `PageModel` class that holds its handlers, such as `OnGet` and `OnPost`. The URL comes from the file path, so this page answers `/Shelf`. It needs `builder.Services.AddRazorPages()` and `app.MapRazorPages()`, which BookShelf already has for the Identity pages.

Pick Razor Pages when your app is mostly separate pages that each read or edit one thing. Pick MVC when many views share controllers, filters, and models, or when the app also serves an API. This guide uses MVC, and all the Razor syntax on this page works the same in both.

## Learn more

- [Views](/mvc/views)
- [Layouts and partials](/mvc/layouts)
- [Forms](/mvc/forms)
- [View components and tag helpers](/mvc/view-components)
- [Razor syntax reference for ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/views/razor)
