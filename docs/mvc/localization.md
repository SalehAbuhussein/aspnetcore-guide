# Localization

## Introduction

Localization shows your app in the user's language and formats dates and numbers the way they expect. This page adds English and Arabic to BookShelf: translated text in `.resx` files, translated validation messages, a language switcher, and right-to-left layout for Arabic.

Two words you will see: culture controls formatting of dates, numbers, and currency. UI culture controls which translated text is used. They are usually set together.

## Registering the services

```csharp [Program.cs]
using Microsoft.AspNetCore.Mvc.Razor;

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

string[] cultures = ["en", "ar"];
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en")
        .AddSupportedCultures(cultures)
        .AddSupportedUICultures(cultures);
});

var app = builder.Build();

// ...
app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseRouting();
```

- `AddLocalization` registers `IStringLocalizer` and says translations live in the `Resources` folder.
- `AddViewLocalization` registers `IViewLocalizer` for views.
- `AddDataAnnotationsLocalization` translates `[Required]`, `[Display]`, and other attribute messages.
- `RequestLocalizationOptions` lists the cultures you support. Requests for any other culture fall back to `en`.

`UseRequestLocalization()` picks the culture for each request. It must come before `UseRouting()` and anything that renders output. By default it checks, in order, a `?culture=ar` query string, a culture cookie, and the browser's `Accept-Language` header.

## Resource files

A `.resx` file is an XML file of name and value pairs. You keep one per class or view per language. The English text is the key itself, so you only need files for the other languages.

```text
Resources/
├── Controllers/
│   └── BooksController.ar.resx
├── Views/
│   └── Books/
│       └── Index.ar.resx
└── Models.Book.ar.resx
```

The file name is the full name of the class or view without the `BookShelf.` root namespace, then the culture, then `.resx`. Each dot can also be a folder, so `Resources/Controllers/BooksController.ar.resx` and `Resources/Controllers.BooksController.ar.resx` both work.

```xml [Resources/Views/Books/Index.ar.resx]
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <data name="Books" xml:space="preserve"><value>الكتب</value></data>
  <data name="Title" xml:space="preserve"><value>العنوان</value></data>
  <data name="Author" xml:space="preserve"><value>المؤلف</value></data>
  <data name="Add a book" xml:space="preserve"><value>أضف كتابًا</value></data>
</root>
```

Visual Studio and Rider have a table editor for `.resx` files, so you rarely edit the XML by hand. In VS Code, copy this file as a starting point.

## Translating views

```razor [Views/_ViewImports.cshtml]
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer
```

Adding the `@inject` line here makes `Localizer` available in every view.

```razor [Views/Books/Index.cshtml]
@{
    ViewData["Title"] = Localizer["Books"];
}

<h1>@Localizer["Books"]</h1>
<a asp-action="Create" class="btn btn-primary">@Localizer["Add a book"]</a>
```

`Localizer["Books"]` looks up `Books` in `Resources/Views/Books/Index.ar.resx` when the UI culture is Arabic. When there is no file or no matching key, it returns the key itself, so English pages need no resource file.

## Translating controllers

```csharp [Controllers/BooksController.cs]
using Microsoft.Extensions.Localization;

public class BooksController(AppDbContext db, IStringLocalizer<BooksController> localizer) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Book book)
    {
        if (!ModelState.IsValid) return View(book);

        db.Books.Add(book);
        await db.SaveChangesAsync();
        TempData["Message"] = localizer["Book added."].Value;
        return RedirectToAction(nameof(Index));
    }
}
```

`IStringLocalizer<BooksController>` reads from `Resources/Controllers/BooksController.ar.resx`. `.Value` gives the plain string, which you need when storing it in `TempData`.

## Translating validation messages

```csharp [Models/Book.cs]
[Required(ErrorMessage = "The title is required.")]
[Display(Name = "Title")]
public string Title { get; set; } = "";
```

With `AddDataAnnotationsLocalization`, the `ErrorMessage` and `Display` name are used as keys in `Resources/Models.Book.ar.resx`. Write the message explicitly. The framework's built-in default messages are not looked up in your files.

## Switching language with a cookie

The cookie provider remembers the choice across requests. Add a small controller that sets it:

```csharp [Controllers/CultureController.cs]
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace BookShelf.Controllers;

public class CultureController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

        return LocalRedirect(returnUrl);
    }
}
```

`MakeCookieValue` writes the value in the format the provider expects, `c=ar|uic=ar`, into the `.AspNetCore.Culture` cookie. `LocalRedirect` only allows URLs on your own site, so the form cannot be used to send users elsewhere. An unsupported culture in the cookie is ignored, and the request falls back to `en`.

Put the form in the layout footer:

```razor [Views/Shared/_Layout.cshtml]
<form asp-controller="Culture" asp-action="Set" method="post" class="d-inline">
    <input type="hidden" name="returnUrl" value="@(Context.Request.Path + Context.Request.QueryString)" />
    <button name="culture" value="en" class="btn btn-link btn-sm">English</button>
    <button name="culture" value="ar" class="btn btn-link btn-sm">العربية</button>
</form>
```

Each button submits its own `value` as `culture`. The form tag helper adds the antiforgery token.

## Dates and numbers

Formatting follows the current culture automatically:

```razor
<p>@DateTime.Today.ToString("D")</p>
<td>@book.Price.ToString("N2")</td>
```

With `en`, `"D"` prints `Monday, October 19, 2026`. With `ar` it prints the Arabic day and month names, such as `الاثنين، 19 أكتوبر 2026`, and the decimal separator changes too.

::: warning
`"C"` (currency) prints a generic `¤` sign with neutral cultures like `en` and `ar`, because they have no country. Use specific cultures such as `en-US` and `ar-EG` when you show prices. Some specific cultures change more than you expect: `ar-SA` uses the Hijri calendar for dates by default.
:::

## Right-to-left layout for Arabic

Arabic reads right to left. Set `dir` on the `<html>` element from the current culture:

```razor [Views/Shared/_Layout.cshtml]
@using System.Globalization
@{
    var culture = CultureInfo.CurrentUICulture;
}
<!DOCTYPE html>
<html lang="@culture.Name" dir="@(culture.TextInfo.IsRightToLeft ? "rtl" : "ltr")">
```

The browser then mirrors text, tables, and form fields. Bootstrap 5 ships a right-to-left stylesheet, `bootstrap.rtl.min.css`, in the same folder as `bootstrap.min.css`. Link it instead of the normal one when `IsRightToLeft` is true, so margins, padding, and the navbar flip too.

## Learn more

- [Validation](/mvc/validation)
- [Views](/mvc/views)
- [Globalization and localization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/localization)
