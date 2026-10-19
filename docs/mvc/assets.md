# CSS and JavaScript

## Introduction

The MVC template already ships with Bootstrap, jQuery, and two empty files for your own CSS and JavaScript. This page shows what you get, how to scope CSS to one view, how to add a library with LibMan, and when to reach for npm and Vite instead.

## What the template ships

```text
wwwroot/
├── css/site.css
├── js/site.js
└── lib/
    ├── bootstrap/
    ├── jquery/
    ├── jquery-validation/
    └── jquery-validation-unobtrusive/
```

`wwwroot/lib` holds copies of Bootstrap and jQuery. They are plain files checked into your project, with no package manager behind them. The layout links them:

```razor [Views/Shared/_Layout.cshtml]
<link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
<link rel="stylesheet" href="~/BookShelf.styles.css" asp-append-version="true" />
...
<script src="~/lib/jquery/dist/jquery.min.js"></script>
<script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
<script src="~/js/site.js" asp-append-version="true"></script>
@await RenderSectionAsync("Scripts", required: false)
```

Write site-wide styles in `site.css` and site-wide scripts in `site.js`. The jQuery validation libraries are used by forms through `_ValidationScriptsPartial`. See [Validation](/mvc/validation).

## Fingerprinting recap

`app.MapStaticAssets()` serves everything in `wwwroot`, compresses it on publish, and sets caching headers. `asp-append-version="true"` adds a hash of the file's contents to the URL, so the browser downloads a new copy whenever the file changes. The [Static files](/mvc/static-files) page covers both in detail.

## CSS isolation

CSS isolation lets you write styles that apply to one view only. Create a `.css` file next to the view, with the same name plus `.css`:

```css [Views/Books/Index.cshtml.css]
h1 {
    color: #6f42c1;
}
```

At build time, ASP.NET Core adds a unique attribute to every element in `Index.cshtml`, such as `<h1 b-hh84u78bd8>`, and rewrites the selector to `h1[b-hh84u78bd8]`. All isolated files are bundled into one file named after your project, `BookShelf.styles.css`. The template's layout already links it, so the purple heading shows on the books list and nowhere else.

The template uses this for `Views/Shared/_Layout.cshtml.css`. Isolated styles only reach elements in that view. They do not reach elements rendered by a partial or a view component inside it.

## Using @section Scripts

The layout renders `@await RenderSectionAsync("Scripts", required: false)` after its own scripts. A view fills that slot to add a script only on that page:

```razor [Views/Books/Create.cshtml]
@section Scripts {
    <partial name="_ValidationScriptsPartial" />
    <script src="~/js/create-book.js" asp-append-version="true"></script>
}
```

Because the section renders after jQuery and `site.js`, your page script can use them. `required: false` means views that do not define the section still work. See [Layouts and partials](/mvc/layouts).

## Adding a library with LibMan

LibMan (Library Manager) downloads files from a CDN into `wwwroot/lib`. It suits apps that need a few ready-made files and no build step. Install the command line tool once:

```bash
dotnet tool install -g Microsoft.Web.LibraryManager.Cli
```

Then, from the project folder:

```bash
libman init -p jsdelivr
libman install htmx.org@2.0.11 --destination wwwroot/lib/htmx --files dist/htmx.min.js
```

`libman init` creates `libman.json` with jsDelivr as the default source. `libman install` downloads only the files you list and records the library in `libman.json`:

```json [libman.json]
{
  "version": "3.0",
  "defaultProvider": "jsdelivr",
  "libraries": [
    {
      "library": "htmx.org@2.0.11",
      "destination": "wwwroot/lib/htmx",
      "files": [
        "dist/htmx.min.js"
      ]
    }
  ]
}
```

Link the file like any other:

```razor
<script src="~/lib/htmx/dist/htmx.min.js"></script>
```

Commit `libman.json`. A teammate can run `libman restore` to download the same files. To update, change the version in `libman.json` and run `libman restore` again.

## npm and Vite for a build step

When you want TypeScript, npm packages that import each other, or bundling several files into one, use npm with Vite. Vite builds your sources into plain files in `wwwroot`, and ASP.NET Core serves them like any other static file. You need Node.js installed.

```bash
mkdir ClientApp
cd ClientApp
npm init -y
npm install -D vite typescript
```

```js [ClientApp/vite.config.js]
import { defineConfig } from "vite";

export default defineConfig({
  build: {
    outDir: "../wwwroot/dist",
    emptyOutDir: true,
    manifest: false,
    rollupOptions: {
      input: "src/main.ts",
      output: {
        entryFileNames: "app.js",
        assetFileNames: "app.[ext]",
      },
    },
  },
});
```

```ts [ClientApp/src/main.ts]
import "./app.css";

const count: number = document.querySelectorAll("#book-rows tr").length;
console.log(`BookShelf has ${count} books on this page`);
```

Add `"type": "module"` and a `"build": "vite build"` script to `ClientApp/package.json`, then run `npm run build`. Vite writes `wwwroot/dist/app.js` and `wwwroot/dist/app.css`.

The output names have no hash, because `asp-append-version` and `MapStaticAssets` already handle cache busting. That is why `manifest` is off: you never need to look up a hashed file name. Link the files from the layout:

```razor [Views/Shared/_Layout.cshtml]
<link rel="stylesheet" href="~/dist/app.css" asp-append-version="true" />
<script type="module" src="~/dist/app.js" asp-append-version="true"></script>
```

Vite outputs an ES module, so the script tag needs `type="module"`. Run `npm run build` before `dotnet build` or `dotnet publish`, so the files exist when `MapStaticAssets` records them. Add `wwwroot/dist/` and `ClientApp/node_modules/` to `.gitignore`.

::: tip
Start with LibMan or a plain `<script>` tag. Move to Vite when you actually write TypeScript or need to bundle npm packages.
:::

## Learn more

- [Static files](/mvc/static-files)
- [Layouts and partials](/mvc/layouts)
- [Interactive pages with htmx](/mvc/htmx)
- [Use the LibMan CLI with ASP.NET Core](https://learn.microsoft.com/aspnet/core/client-side/libman/libman-cli)
