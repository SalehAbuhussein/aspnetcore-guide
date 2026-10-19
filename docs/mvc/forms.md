# Forms

## Introduction

Forms let users send data to your app. MVC tag helpers build the form HTML from your model, and model binding turns the submitted fields back into a C# object.

## The controller actions

```csharp [Controllers/BooksController.cs]
[HttpGet]
public IActionResult Create()
{
    return View(new Book());
}

[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult Create(Book book)
{
    book.Id = _books.Count == 0 ? 1 : _books.Max(b => b.Id) + 1;
    _books.Add(book);

    TempData["Message"] = $"Added \"{book.Title}\".";
    return RedirectToAction(nameof(Index));
}
```

The GET action shows an empty form. The POST action receives the submitted book, adds it to the in-memory list from the [Controllers](/mvc/controllers) page, and redirects. The [CRUD](/mvc/crud) page does the same against the database.

## The form view

```razor [Views/Books/Create.cshtml]
@model Book

@{
    ViewData["Title"] = "Add a book";
}

<h1>Add a book</h1>

<form asp-action="Create" method="post">
    <div class="mb-3">
        <label asp-for="Title" class="form-label"></label>
        <input asp-for="Title" class="form-control" />
    </div>
    <div class="mb-3">
        <label asp-for="Author" class="form-label"></label>
        <input asp-for="Author" class="form-control" />
    </div>
    <div class="mb-3">
        <label asp-for="Year" class="form-label"></label>
        <input asp-for="Year" class="form-control" />
    </div>
    <button type="submit" class="btn btn-primary">Save</button>
</form>
```

`asp-action="Create"` on the `<form>` tag sets the `action` URL to `/Books/Create`. `asp-for="Title"` binds an element to the `Title` property of the model. On a `<label>`, it writes the property name as the text. On an `<input>`, it sets the `name`, `id`, and current `value`. It also picks the input type from the property type, so `Year` becomes `type="number"`.

Because the form is built from the model, a renamed property shows up as a build error instead of a broken form.

## Model binding

When the form is submitted, the browser sends fields like `Title=Dune&Author=Frank+Herbert&Year=1965`. Model binding matches each field name to a property on the `Book` parameter and converts the value to the right type. You do not read the raw request yourself.

Binding also works for simple parameters. `public IActionResult Delete(int id)` gets `id` from the route, the query string, or a form field with that name.

::: warning
Model binding sets every property it finds a matching field for. A user can add an `Id` field to the form by hand. Here the action overwrites `Id` anyway. For forms that edit data, bind to a view model that contains only the fields the user may change. See [Models](/mvc/models).
:::

## Anti-forgery tokens

Cross-site request forgery (CSRF) is an attack where another site tricks a signed-in user's browser into submitting a form to your app. MVC blocks it with a hidden token.

The form tag helper adds the token for you when `method="post"`:

```html
<input name="__RequestVerificationToken" type="hidden" value="CfDJ8..." />
```

`[ValidateAntiForgeryToken]` on the POST action checks it. If the token is missing or wrong, the request fails with 400 Bad Request. Put the attribute on every POST action that changes data.

## Post, redirect, get

The POST action ends with `RedirectToAction` instead of returning a view. This is the Post/Redirect/Get pattern, PRG for short. After a successful POST, the browser follows the redirect and makes a fresh GET request. If the user then refreshes the page, they reload the list instead of submitting the form a second time.

To show a confirmation after the redirect, store it in `TempData`. TempData is a dictionary that keeps a value until the next request reads it. `ViewData` would not survive the redirect.

Read it in the list view, or in the layout so every page can show it:

```razor [Views/Books/Index.cshtml]
@if (TempData["Message"] is string message)
{
    <div class="alert alert-success">@message</div>
}
```

Once read, the value is removed, so the message appears once.

## Next steps

This form accepts anything, including a blank title. The [Validation](/mvc/validation) page adds rules and error messages.

## Learn more

- [Tag Helpers in forms in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/views/working-with-forms)
- [Prevent Cross-Site Request Forgery (XSRF/CSRF) attacks in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery)
