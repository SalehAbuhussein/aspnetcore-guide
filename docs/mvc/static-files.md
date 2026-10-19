# Static files

## Introduction

Static files are files the server sends to the browser without changing them, such as CSS, JavaScript, images, and fonts. In an ASP.NET Core app they live in the `wwwroot` folder.

## The wwwroot folder

```text
wwwroot/
├── css/
│   └── site.css
├── js/
│   └── site.js
├── lib/
│   ├── bootstrap/
│   ├── jquery/
│   ├── jquery-validation/
│   └── jquery-validation-unobtrusive/
└── favicon.ico
```

`wwwroot` is the web root. A file at `wwwroot/css/site.css` is served at the URL `/css/site.css`. Files outside `wwwroot`, like `appsettings.json` or your C# code, are never served.

Put your own styles in `wwwroot/css/site.css` and your own scripts in `wwwroot/js/site.js`. Add images in a new folder, such as `wwwroot/images/`.

## MapStaticAssets

```csharp [Program.cs]
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
```

`MapStaticAssets()` serves the files in `wwwroot`. It was added in .NET 9 and the template uses it. At build time it records every file in `wwwroot`. When the app is published, it compresses files with gzip and Brotli and sends caching headers, so browsers download each file once.

`.WithStaticAssets()` on the controller route connects your views to that build-time list. Views can then link to the exact version of each file.

::: warning
`MapStaticAssets()` only serves files that exist when the app is built. If your app writes new files to disk while running, such as user uploads, serve that folder with `app.UseStaticFiles()` instead.
:::

## Link to files from a view

```razor [Views/Shared/_Layout.cshtml]
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
<img src="~/images/logo.png" alt="BookShelf" />
<script src="~/js/site.js" asp-append-version="true"></script>
```

The `~/` prefix means "the root of the app". Razor turns `~/css/site.css` into `/css/site.css`, and it still works if the app is hosted under a sub path such as `/bookshelf/`.

`asp-append-version="true"` adds a version value based on the file's contents to the URL, such as `/css/site.css?v=Kl_dqr9N...`. When you change the file, the value changes, and browsers download the new copy instead of using an old cached one. This is called cache busting.

::: tip
Add `asp-append-version="true"` to your own CSS and JS files. Library files in `wwwroot/lib` change only when you update the library, so they do not need it.
:::

## Bootstrap

The template includes Bootstrap, a CSS framework with ready-made styles for layout, buttons, forms, and navigation. Its files are in `wwwroot/lib/bootstrap/dist/`, and `_Layout.cshtml` already loads them:

```razor [Views/Shared/_Layout.cshtml]
<link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
<script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
```

That is why classes like `btn btn-primary` and `form-control` on the [Forms](/mvc/forms) page work with no extra setup. To remove Bootstrap, delete these lines and the `wwwroot/lib/bootstrap` folder, then replace the classes in the template's views.

## Scoped CSS

The template layout also loads `~/BookShelf.styles.css`. The build generates this file from CSS isolation files, which are stylesheets named after a view, such as `Views/Books/Index.cshtml.css`. Styles in that file apply only to that view. You do not create `BookShelf.styles.css` yourself.

## Learn more

- [Static files in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/static-files)
