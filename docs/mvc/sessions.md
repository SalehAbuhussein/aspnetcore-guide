# Sessions and cookies

## Introduction

HTTP forgets everything between requests. Sessions and cookies let your app remember small things about a visitor, such as the books they looked at last. This page shows both, and when to pick each.

## Turning on sessions

```csharp [Program.cs (excerpt)]
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ...

app.UseRouting();

app.UseAuthorization();
app.UseSession();

app.MapStaticAssets();
```

A session is a small store of data on the server that belongs to one visitor. The browser gets a cookie with a session id, and the server uses that id to find the visitor's data. `AddDistributedMemoryCache` gives the session somewhere to keep the data, here in the app's memory.

`UseSession` must come after `UseRouting` and before `MapControllerRoute`. `IdleTimeout` ends the session after 30 minutes without a request. `IsEssential` lets the session cookie work even before a visitor accepts a cookie consent banner.

## Reading and writing session values

```csharp [Controllers/BooksController.cs (excerpt)]
HttpContext.Session.SetString("LastSearch", "dune");
HttpContext.Session.SetInt32("Visits", 3);

string? lastSearch = HttpContext.Session.GetString("LastSearch");
int? visits = HttpContext.Session.GetInt32("Visits");

HttpContext.Session.Remove("LastSearch");
```

Session values are strings, integers, or raw bytes, stored under a key you choose. `GetString` returns `null` when the key does not exist. To store a list or an object, turn it into a JSON string first.

## Example: recently viewed books

Show the last five books a visitor opened. Store their ids in the session as JSON each time the Details action runs.

```csharp [Controllers/BooksController.cs (excerpt)]
using System.Text.Json; // at the top of the file

// GET: /Books/Details/5
public IActionResult Details(int id)
{
    var book = _books.FirstOrDefault(b => b.Id == id);
    if (book is null) return NotFound();

    var json = HttpContext.Session.GetString("RecentBooks");
    List<int> recent = json is null ? [] : JsonSerializer.Deserialize<List<int>>(json)!;

    recent.Remove(id);
    recent.Insert(0, id);
    if (recent.Count > 5) recent.RemoveAt(5);

    HttpContext.Session.SetString("RecentBooks", JsonSerializer.Serialize(recent));

    ViewData["Recent"] = _books
        .Where(b => recent.Contains(b.Id) && b.Id != id)
        .ToList();

    return View(book);
}
```

`JsonSerializer` turns the list of ids into a string and back. The book being viewed moves to the front of the list, and the list never grows past five. The query loads the other recent books for the view. `_books` is the in-memory list from the [Controllers](/mvc/controllers) page. The [Database](/mvc/database) pages replace that list with `db.Books`, and the session code stays the same.

```razor [Views/Books/Details.cshtml (excerpt)]
@if (ViewData["Recent"] is List<BookShelf.Models.Book> recent && recent.Count > 0)
{
    <h2>Recently viewed</h2>
    <ul>
        @foreach (var b in recent)
        {
            <li><a asp-action="Details" asp-route-id="@b.Id">@b.Title</a></li>
        }
    </ul>
}
```

::: warning
The memory cache is lost when the app restarts, and it is not shared between servers. If you run more than one copy of the app, register a shared cache such as Redis instead of `AddDistributedMemoryCache`. See [Caching](/mvc/caching).
:::

## TempData

TempData holds a value for exactly one more request, which makes it right for "Book added" messages after a redirect. You met it on the [Forms](/mvc/forms) page. When sessions are turned on you can store TempData in the session with `AddControllersWithViews().AddSessionStateTempDataProvider()`. The default keeps it in a cookie, which works fine for short messages.

## Cookies

Use a cookie when the value is small, not secret, and should outlive the session, such as a theme choice.

```csharp [Controllers/HomeController.cs (excerpt)]
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult SetTheme(string theme)
{
    Response.Cookies.Append("theme", theme, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddDays(365)
    });

    return RedirectToAction(nameof(Index));
}
```

`Response.Cookies.Append` sends a cookie to the browser. Read it on later requests with `Request.Cookies["theme"]`, which returns `null` when the cookie is missing. Remove it with `Response.Cookies.Delete("theme")`.

The `CookieOptions` settings matter:

- `HttpOnly = true` hides the cookie from JavaScript, so a script injected into the page cannot steal it.
- `Secure = true` sends the cookie only over HTTPS.
- `SameSite = SameSiteMode.Lax` stops the browser from sending the cookie on most requests that start on another site, which helps against cross-site request forgery.
- `Expires` keeps the cookie after the browser closes. Without it, the cookie is deleted when the browser closes.

Cookies are stored on the visitor's computer, and they can read and change them. Never put anything in a plain cookie that you would not show the user. Use the session or the database for anything you must trust.

## Cookie consent

Laws such as the GDPR in the EU require consent before setting cookies that are not needed for the site to work. Cookies for sign-in, sessions, and antiforgery count as essential. Cookies for analytics or preferences usually do not. ASP.NET Core has a `CookiePolicyOptions` setting with `CheckConsentNeeded` that blocks non-essential cookies until the visitor agrees. Check the rules that apply to your users before you rely on it.

## Learn more

- [Session and state management in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/app-state)
- [EU General Data Protection Regulation (GDPR) support in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/gdpr)
