# Interactive pages with htmx

## Introduction

htmx is a small JavaScript library that lets HTML attributes send requests and swap the response into the page. Your controllers keep returning HTML from Razor views, and you get live search and inline delete without writing a JavaScript framework app or a JSON API. This page adds both to the books list.

## Adding htmx

Add one script tag to the layout, after the other scripts:

```razor [Views/Shared/_Layout.cshtml]
<script src="https://cdn.jsdelivr.net/npm/htmx.org@2.0.11/dist/htmx.min.js"></script>
@await RenderSectionAsync("Scripts", required: false)
```

Pin an exact version so an update cannot change your app without you knowing. If you prefer to serve the file yourself, install it into `wwwroot/lib` with LibMan, as shown on [CSS and JavaScript](/mvc/assets).

htmx reads `hx-*` attributes. `hx-get`, `hx-post`, and `hx-delete` say which request to send. `hx-target` says which element receives the response, and `hx-swap` says how to put it there.

## Live search

Move the table rows into a partial view, so the controller can return just the rows:

```razor [Views/Books/_BookRows.cshtml]
@model List<Book>

@foreach (var book in Model)
{
    <tr>
        <td>@book.Title</td>
        <td>@book.Author</td>
        <td>@book.Year</td>
        <td>
            <button class="btn btn-sm btn-outline-danger"
                    hx-delete="@Url.Action("Remove", new { id = book.Id })"
                    hx-confirm="Delete @book.Title?"
                    hx-target="closest tr"
                    hx-swap="outerHTML">Delete</button>
        </td>
    </tr>
}
```

The Index view renders the partial inside `<tbody>` and adds a search box:

```razor [Views/Books/Index.cshtml]
@model List<Book>

<h1>Books</h1>

<input type="search" name="q" value="@ViewData["Query"]"
       class="form-control mb-3" placeholder="Search by title or author"
       hx-get="@Url.Action("Index")"
       hx-trigger="keyup changed delay:300ms, search"
       hx-target="#book-rows" />

<table class="table">
    <thead>
        <tr><th>Title</th><th>Author</th><th>Year</th><th></th></tr>
    </thead>
    <tbody id="book-rows">
        <partial name="_BookRows" model="Model" />
    </tbody>
</table>
```

`hx-get` sends a GET request to `/Books` and includes the input's value as `?q=...`, because the input has a `name`. `hx-trigger="keyup changed delay:300ms"` waits until the user stops typing for 300 milliseconds, and only fires if the value changed. The extra `search` trigger fires when the user clears the box with its x button. `hx-target="#book-rows"` puts the response inside the `<tbody>`.

## Returning a partial or a full page

htmx adds an `HX-Request: true` header to every request it sends. The action checks for it:

```csharp [Controllers/BooksController.cs]
// GET: /Books?q=dune
public async Task<IActionResult> Index(string? q)
{
    var query = db.Books.AsQueryable();
    if (!string.IsNullOrWhiteSpace(q))
    {
        query = query.Where(b => EF.Functions.Like(b.Title, $"%{q}%")
                              || EF.Functions.Like(b.Author, $"%{q}%"));
    }

    var books = await query.OrderBy(b => b.Title).ToListAsync();

    if (Request.Headers.ContainsKey("HX-Request"))
    {
        return PartialView("_BookRows", books);
    }

    ViewData["Query"] = q;
    return View(books);
}
```

`PartialView` renders only `_BookRows.cshtml`, without the layout. A normal visit to `/Books?q=dune` still gets the full page, so the search URL works when bookmarked or opened in a new tab. `EF.Functions.Like` makes the search case-insensitive in SQLite, where `Contains` is case-sensitive.

## Inline delete

The Delete button in `_BookRows` sends `DELETE /Books/Remove/5`. `hx-confirm` shows a browser confirm box first. `hx-target="closest tr"` with `hx-swap="outerHTML"` replaces the whole row with the response. The action returns an empty 200 response, so the row disappears.

```csharp [Controllers/BooksController.cs]
// DELETE: /Books/Remove/5  (called by htmx)
[HttpDelete]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Remove(int id)
{
    var book = await db.Books.FindAsync(id);
    if (book is not null)
    {
        db.Books.Remove(book);
        await db.SaveChangesAsync();
    }
    return Ok();
}
```

The action is named `Remove` because the existing GET `Delete(int id)` action has the same parameters, and C# does not allow two methods with the same name and parameters.

### Sending the antiforgery token

`[ValidateAntiForgeryToken]` rejects the request with 400 unless it carries a valid token. A form tag helper adds the token as a hidden field, but an htmx button is not inside a form. Instead, put the token in the layout and send it as a header on every htmx request.

```razor [Views/Shared/_Layout.cshtml]
@inject Microsoft.AspNetCore.Antiforgery.IAntiforgery Antiforgery
<!DOCTYPE html>
<html lang="en">
<head>
    <meta name="request-verification-token"
          content="@Antiforgery.GetAndStoreTokens(Context).RequestToken" />
    ...
</head>
<body>
    ...
    <script src="https://cdn.jsdelivr.net/npm/htmx.org@2.0.11/dist/htmx.min.js"></script>
    <script>
        document.addEventListener("htmx:configRequest", (event) => {
            const token = document.querySelector('meta[name="request-verification-token"]').content;
            event.detail.headers["RequestVerificationToken"] = token;
        });
    </script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

`GetAndStoreTokens` creates the token and sets the matching antiforgery cookie. htmx raises `htmx:configRequest` before each request, and the listener adds the token as a `RequestVerificationToken` header. ASP.NET Core reads that header name by default, so `[ValidateAntiForgeryToken]` accepts the request. GET requests are not checked, so sending the header on them does no harm.

::: tip
For a single button you can use `hx-headers` instead of the listener: `hx-headers='{"RequestVerificationToken": "@Antiforgery.GetAndStoreTokens(Context).RequestToken"}'`, with the same `@inject` line in that view. The listener is simpler once several elements need it.
:::

## Learn more

- [CRUD](/mvc/crud)
- [Layouts and partials](/mvc/layouts)
- [Security hardening](/mvc/security-hardening)
- [htmx documentation](https://htmx.org/docs/)
- [Prevent cross-site request forgery attacks in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery)
