# File uploads

## Introduction

This page lets users upload a cover image for each book. You will add a column for the image path, accept the file in a form, check it, save it under `wwwroot/uploads`, and show it on the Details page.

## Adding the cover column

```csharp [Models/Book.cs (excerpt)]
public string? CoverPath { get; set; }
```

```bash
dotnet ef migrations add AddBookCover
dotnet ef database update
```

`CoverPath` stores the image's URL, such as `/uploads/3f2a....jpg`, not the image itself. Files belong on disk or in blob storage. The database only keeps track of where they are. The `?` makes the column nullable, because existing books have no cover.

## The upload form

```razor [Views/Books/Create.cshtml]
@model BookShelf.Models.Book

<h1>Add a book</h1>

<form asp-action="Create" method="post" enctype="multipart/form-data">
    <div asp-validation-summary="All"></div>
    <label asp-for="Title"></label>
    <input asp-for="Title" />
    <label asp-for="Author"></label>
    <input asp-for="Author" />
    <label asp-for="Year"></label>
    <input asp-for="Year" />
    <label for="cover">Cover image</label>
    <input id="cover" name="cover" type="file" accept=".jpg,.jpeg,.png,.webp" />
    <button type="submit">Save</button>
</form>
```

`enctype="multipart/form-data"` tells the browser to send the file's bytes along with the other fields. Without it, the browser sends only the file name. The `accept` attribute filters the file picker, but it is only a hint. Anyone can send any file to your server.

## Receiving the file

```csharp [Controllers/BooksController.cs (excerpt)]
public class BooksController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxCoverBytes = 2 * 1024 * 1024; // 2 MB

    // POST: /Books/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Create(Book book, IFormFile? cover)
    {
        if (cover is not null)
        {
            var extension = Path.GetExtension(cover.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
                ModelState.AddModelError("", "The cover must be a JPG, PNG, or WebP image.");
            else if (cover.Length > MaxCoverBytes)
                ModelState.AddModelError("", "The cover must be 2 MB or smaller.");
        }

        if (!ModelState.IsValid) return View(book);

        if (cover is not null)
        {
            book.CoverPath = await SaveCoverAsync(cover);
        }

        db.Books.Add(book);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> SaveCoverAsync(IFormFile cover)
    {
        var extension = Path.GetExtension(cover.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{extension}";
        var folder = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(folder);

        await using var stream = System.IO.File.Create(Path.Combine(folder, fileName));
        await cover.CopyToAsync(stream);

        return $"/uploads/{fileName}";
    }

    // other actions as before
}
```

`IFormFile` is the uploaded file. Model binding fills it from the form field with the same name, `cover`. It is nullable because the cover is optional.

`IWebHostEnvironment` is a built-in service that knows where the app lives. `WebRootPath` is the full path to `wwwroot`. `Directory.CreateDirectory` does nothing if the folder already exists. The code writes `System.IO.File` in full, because inside a controller `File` means the method that returns a file download.

## Never trust the client file name

The file name comes from the user's computer, and an attacker can set it to anything, such as `../../Program.cs` or a name with odd characters. The code only reads the extension from it, checks that against a short list, and then makes a new name with `Guid.NewGuid()`. A GUID is a random 128-bit value, so names never clash and cannot be guessed.

::: warning
An extension check stops honest mistakes, not a determined attacker. A file named `cover.png` can contain anything. Never save uploads anywhere the server would run them, and for higher risk apps check the first bytes of the file or process images with an image library before saving.
:::

## Showing the image

```razor [Views/Books/Details.cshtml (excerpt)]
@if (Model.CoverPath is not null)
{
    <img src="@Model.CoverPath" alt="Cover of @Model.Title" width="200" />
}
```

`MapStaticAssets` in the template only serves files that existed at build time. Add `UseStaticFiles` so the app also serves files written while it runs.

```csharp [Program.cs (excerpt)]
app.UseStaticFiles();
app.MapStaticAssets();
```

See [Static files](/mvc/static-files) for the difference between the two.

## Keeping the cover on edit

```razor [Views/Books/Edit.cshtml (excerpt)]
<input type="hidden" asp-for="Id" />
<input type="hidden" asp-for="CoverPath" />
```

The Edit form has no cover field, so without this hidden input the POST `Edit` action receives a `Book` with `CoverPath` set to `null`. `db.Books.Update(book)` then saves that `null` and the book loses its cover. Add the line under the hidden `Id` input.

## Size limits

ASP.NET Core rejects large requests before your code runs. Kestrel, the built-in web server, allows request bodies up to about 30 MB by default. `[RequestSizeLimit(5 * 1024 * 1024)]` lowers that to 5 MB for this one action. A request over the limit fails with status 413 Payload Too Large.

Keep two limits. `RequestSizeLimit` protects the server from huge requests. The `MaxCoverBytes` check in the action gives users a clear error message for files that are a bit too big.

In production, store uploads in blob storage such as Azure Blob Storage or Amazon S3 instead of `wwwroot`. Files in `wwwroot` are lost when you redeploy and are not shared between servers. Old covers from deleted books can be cleaned up with a [background task](/mvc/background-tasks).

## Learn more

- [Upload files in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads)
- [Static files in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/static-files)
