# Validation

## Introduction

Validation checks that submitted data follows your rules before you save it. In MVC you declare the rules as attributes on the model. The framework checks them on the server and, with a script included, in the browser too.

## Add rules to the model

```csharp [Models/Book.cs]
using System.ComponentModel.DataAnnotations;

namespace BookShelf.Models;

public class Book
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string Author { get; set; } = "";

    [Range(1450, 2100, ErrorMessage = "Enter a year between 1450 and 2100.")]
    public int Year { get; set; }
}
```

These attributes are called data annotations. Each one describes a rule:

- `[Required]` rejects an empty value.
- `[StringLength(200)]` rejects text longer than 200 characters.
- `[Range(1450, 2100)]` rejects numbers outside that range.

Every attribute has a default error message. Set `ErrorMessage` to write your own. Other common ones are `[EmailAddress]`, `[Url]`, `[Compare]`, and `[Display(Name = "...")]`, which changes the label text.

## Check ModelState

```csharp [Controllers/BooksController.cs (excerpt)]
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult Create(Book book)
{
    if (!ModelState.IsValid)
    {
        return View(book);
    }

    book.Id = _books.Count == 0 ? 1 : _books.Max(b => b.Id) + 1;
    _books.Add(book);

    TempData["Message"] = $"Added \"{book.Title}\".";
    return RedirectToAction(nameof(Index));
}
```

`ModelState` holds the result of model binding and validation for the current request. MVC runs the checks before your action starts, so all you do is read `ModelState.IsValid`. If it is `false`, return the same view with the submitted `book`. The form redisplays with the user's input and the errors.

`_books` is the in-memory list from the [Controllers](/mvc/controllers) page. The [Database](/mvc/database) and [CRUD](/mvc/crud) pages replace it with `db.Books`, and the `ModelState` check stays the same.

::: warning
Always check `ModelState.IsValid` on the server, even with browser validation turned on. Anyone can turn off JavaScript or send a request without a browser.
:::

## Show errors in the view

```razor [Views/Books/Create.cshtml]
@model Book

<h1>Add a book</h1>

<form asp-action="Create" method="post">
    <div asp-validation-summary="ModelOnly" class="text-danger"></div>

    <div class="mb-3">
        <label asp-for="Title" class="form-label"></label>
        <input asp-for="Title" class="form-control" />
        <span asp-validation-for="Title" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Author" class="form-label"></label>
        <input asp-for="Author" class="form-control" />
        <span asp-validation-for="Author" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Year" class="form-label"></label>
        <input asp-for="Year" class="form-control" />
        <span asp-validation-for="Year" class="text-danger"></span>
    </div>
    <button type="submit" class="btn btn-primary">Save</button>
</form>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

`asp-validation-for` shows the error for one property next to its field. `asp-validation-summary` shows a list of errors in one place. Its value controls what it lists:

- `ModelOnly` shows errors that do not belong to one property.
- `All` shows every error, including property errors.
- `None` shows nothing.

You can add your own error from the controller. Use an empty key for a model level error, or a property name to attach it to a field:

```csharp
if (_books.Any(b => b.Title == book.Title))
{
    ModelState.AddModelError(nameof(Book.Title), "That book is already on your shelf.");
}
```

Add this before the `IsValid` check, so it counts.

## Client-side validation

The `Scripts` section at the end of the view renders `_ValidationScriptsPartial`. The template ships this partial in `Views/Shared/`. It loads jQuery Validation and jQuery Validation Unobtrusive from `wwwroot/lib`.

With those scripts on the page, the browser checks the same rules before the form is sent. The tag helpers write the rules into `data-val-*` attributes on each input, based on your data annotations. You do not write any JavaScript. The user sees errors as they type, and the server still validates on submit.

Include the partial only on pages with forms, which is why it goes in a section instead of the layout. See [Layouts and partials](/mvc/layouts).

## Learn more

- [Model validation in ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/mvc/models/validation)
