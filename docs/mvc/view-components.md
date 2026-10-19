# View components and tag helpers

## Introduction

Razor gives you three ways to reuse a piece of UI: partial views, view components, and tag helpers. This page compares them, builds a "recently added books" view component, and writes a small `<rating>` tag helper.

## Which one to pick

| Tool | What it is | Pick it when |
| --- | --- | --- |
| Partial view | A `.cshtml` file rendered inside another view | You repeat markup and the parent already has the data |
| View component | A C# class plus its own `.cshtml` view | The widget needs to load its own data, on many pages |
| Tag helper | A C# class that changes or produces one HTML element | You want a custom element or attribute, with little or no markup |

A partial cannot load data. It only shows what its parent passes in. See [Layouts and partials](/mvc/layouts). A view component can ask for services like `AppDbContext`, so the layout can show it without every controller sending the data. A tag helper works at the level of one HTML tag, such as turning `<rating value="4" />` into a row of stars.

## A view component

The class name ends in `ViewComponent`. MVC finds it by that suffix, and you refer to it without the suffix.

```csharp [ViewComponents/RecentBooksViewComponent.cs]
using BookShelf.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.ViewComponents;

public class RecentBooksViewComponent(AppDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int count = 5)
    {
        var books = await db.Books
            .OrderByDescending(b => b.Id)
            .Take(count)
            .ToListAsync();

        return View(books);
    }
}
```

The constructor asks for `AppDbContext` through [dependency injection](/mvc/dependency-injection), the same way a controller does. `InvokeAsync` runs each time the component is rendered. Its parameters become the values you pass in. Use `Invoke` instead of `InvokeAsync` when the work is not asynchronous. A component has one or the other, never both.

`View(books)` renders the component's view with `books` as its model. MVC looks for it at `Views/Shared/Components/RecentBooks/Default.cshtml`, or under `Views/{Controller}/Components/RecentBooks/` first if you want a version for one controller.

```razor [Views/Shared/Components/RecentBooks/Default.cshtml]
@model List<Book>

<aside class="card">
    <div class="card-body">
        <h2 class="h6">Recently added</h2>
        <ul class="list-unstyled mb-0">
            @foreach (var book in Model)
            {
                <li><a asp-controller="Books" asp-action="Details" asp-route-id="@book.Id">@book.Title</a></li>
            }
        </ul>
    </div>
</aside>
```

### Rendering the component

There are two ways to render it. The tag form reads like HTML:

```razor
<vc:recent-books count="5" />
```

The method form works anywhere you can write C#:

```razor
@await Component.InvokeAsync("RecentBooks", new { count = 5 })
```

The tag form uses kebab case: `RecentBooks` becomes `recent-books`, and the `count` parameter becomes the `count` attribute. Visual Studio and VS Code complete the attributes for you, and a typo is a build error. The tag form needs one line in `_ViewImports.cshtml`, shown below.

::: tip
Put `<vc:recent-books count="5" />` in `Views/Shared/_Layout.cshtml` to show it on every page. No controller has to change.
:::

## A custom tag helper

A tag helper is a class that Razor runs when it meets a matching element. This one turns `<rating value="4" />` into stars.

```csharp [TagHelpers/RatingTagHelper.cs]
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BookShelf.TagHelpers;

public class RatingTagHelper : TagHelper
{
    public int Value { get; set; }
    public int Max { get; set; } = 5;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var value = Math.Clamp(Value, 0, Max);

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "rating");
        output.Attributes.SetAttribute("title", $"{value} out of {Max}");
        output.Content.SetContent(new string('★', value) + new string('☆', Max - value));
    }
}
```

The class name `RatingTagHelper` makes it match `<rating>` elements. Each public property becomes an attribute, so `Value` maps to `value` and `Max` maps to `max`. Razor converts the attribute text to an `int` for you.

Inside `Process`, `output` is the element that will be written to the page. Setting `TagName` to `span` replaces `<rating>` with `<span>`. `TagMode.StartTagAndEndTag` writes `<span>...</span>` even though you wrote a self-closing tag. `SetContent` HTML-encodes the text, so it is safe to put user data in it.

Use it in any view:

```razor [Views/Books/Details.cshtml]
<rating value="4" />
```

The browser receives:

```html
<span class="rating" title="4 out of 5">★★★★☆</span>
```

## Registering with _ViewImports

Built-in tag helpers come from the `Microsoft.AspNetCore.Mvc.TagHelpers` assembly. Your own tag helpers, and the `<vc:...>` tags for your view components, live in your app's assembly, so add a second `@addTagHelper` line with your project name.

```razor [Views/_ViewImports.cshtml]
@using BookShelf
@using BookShelf.Models
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, BookShelf
```

`*` means every tag helper in that assembly. The name after the comma is the assembly name, which matches the project name unless you changed it. Without this line, Razor sends `<rating>` and `<vc:recent-books>` to the browser unchanged, and nothing appears.

::: warning
If you see a raw `<rating value="4">` tag in the page source, the `@addTagHelper *, BookShelf` line is missing or the assembly name is wrong.
:::

## Learn more

- [Layouts and partials](/mvc/layouts)
- [Views](/mvc/views)
- [View components in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/views/view-components)
