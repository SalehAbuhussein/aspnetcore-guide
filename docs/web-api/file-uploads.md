# File uploads

## Introduction

This page lets a client attach one file to a todo and download it again. You will accept the upload with `IFormFile`, check its size and type, save it outside the public web folder, and stream it back from disk.

<ApiStyle />

## Store the file name on the todo

```csharp [Models/Todo.cs (excerpt)]
public string? AttachmentPath { get; set; }
public string? AttachmentName { get; set; }
```

```bash
dotnet ef migrations add AddAttachment
dotnet ef database update
```

The database stores two names. `AttachmentPath` is the name you generate for the file on disk. `AttachmentName` is the name the user uploaded, which you hand back when they download it. The file itself stays on disk, because databases are a poor place for large binary data.

## The upload endpoint

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
// in MapTodoEndpoints
group.MapPost("/{id:int}/attachment", UploadAttachment)
    .DisableAntiforgery();
group.MapGet("/{id:int}/attachment", DownloadAttachment);

// in the TodoEndpoints class
static readonly string[] AllowedExtensions = [".pdf", ".png", ".jpg", ".txt"];
const long MaxFileSize = 5 * 1024 * 1024;

static async Task<Results<NoContent, NotFound, ValidationProblem>> UploadAttachment(
    int id, IFormFile file, AppDbContext db, IWebHostEnvironment env)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return TypedResults.NotFound();
    }

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (file.Length == 0 || file.Length > MaxFileSize)
    {
        return FileError("The file must be between 1 byte and 5 MB.");
    }

    if (!AllowedExtensions.Contains(extension))
    {
        return FileError("Only .pdf, .png, .jpg and .txt files are allowed.");
    }

    var folder = Path.Combine(env.ContentRootPath, "uploads");
    Directory.CreateDirectory(folder);

    var storedName = $"{Guid.NewGuid()}{extension}";
    await using (var stream = File.Create(Path.Combine(folder, storedName)))
    {
        await file.CopyToAsync(stream);
    }

    if (todo.AttachmentPath is not null)
    {
        File.Delete(Path.Combine(folder, todo.AttachmentPath));
    }

    todo.AttachmentPath = storedName;
    todo.AttachmentName = Path.GetFileName(file.FileName);
    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}

static ValidationProblem FileError(string message) =>
    TypedResults.ValidationProblem(new Dictionary<string, string[]>
    {
        ["file"] = [message]
    });
```

`IFormFile` is the uploaded file. ASP.NET Core reads it from a `multipart/form-data` request, the format browsers and HTTP clients use to send files. The parameter name `file` must match the form field name.

`FileError` returns the same 400 ProblemDetails shape as the [Validation](/web-api/validation) page, so clients handle every error the same way.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
// add IWebHostEnvironment to the primary constructor
public class TodosController(AppDbContext db, IWebHostEnvironment env) : ControllerBase

// in the TodosController class
static readonly string[] AllowedExtensions = [".pdf", ".png", ".jpg", ".txt"];
const long MaxFileSize = 5 * 1024 * 1024;

[HttpPost("{id:int}/attachment")]
public async Task<IActionResult> UploadAttachment(int id, IFormFile file)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return NotFound();
    }

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (file.Length == 0 || file.Length > MaxFileSize)
    {
        return FileError("The file must be between 1 byte and 5 MB.");
    }

    if (!AllowedExtensions.Contains(extension))
    {
        return FileError("Only .pdf, .png, .jpg and .txt files are allowed.");
    }

    var folder = Path.Combine(env.ContentRootPath, "uploads");
    Directory.CreateDirectory(folder);

    var storedName = $"{Guid.NewGuid()}{extension}";
    await using (var stream = System.IO.File.Create(Path.Combine(folder, storedName)))
    {
        await file.CopyToAsync(stream);
    }

    if (todo.AttachmentPath is not null)
    {
        System.IO.File.Delete(Path.Combine(folder, todo.AttachmentPath));
    }

    todo.AttachmentPath = storedName;
    todo.AttachmentName = Path.GetFileName(file.FileName);
    await db.SaveChangesAsync();

    return NoContent();
}

ActionResult FileError(string message)
{
    ModelState.AddModelError("file", message);
    return ValidationProblem(ModelState);
}
```

`IFormFile` is the uploaded file. ASP.NET Core reads it from a `multipart/form-data` request, the format browsers and HTTP clients use to send files. The parameter name `file` must match the form field name. `[ApiController]` knows an `IFormFile` comes from the form, so you do not need `[FromForm]` on it. Add `[FromForm]` when you also read plain form fields into a class. `IWebHostEnvironment` tells you where the app lives on disk. It comes in through the constructor like `AppDbContext`.

The code writes `System.IO.File` in full because `ControllerBase` has its own `File` method, which returns a file response. `FileError` adds the message to `ModelState` and returns `ValidationProblem`, which gives the same 400 ProblemDetails shape as the [Validation](/web-api/validation) page, so clients handle every error the same way.

</div>

## Antiforgery and uploads

<div class="only-minimal">

Any Minimal API endpoint that reads form data, including `IFormFile`, gets antiforgery protection by default. Antiforgery stops another website from submitting a form to your app using the visitor's cookies. It needs a token that your own page put in the form.

An API called with a bearer token from JavaScript or a mobile app has no such page and no token, so every upload would fail. `.DisableAntiforgery()` turns the check off for this one endpoint. Keep it on if your API uses cookie [authentication](/web-api/authentication) and is called from browser forms.

</div>

<div class="only-controllers">

Antiforgery stops another website from submitting a form to your app using the visitor's cookies. It needs a token that your own page put in the form. Controller actions do not check it automatically, even when they read `IFormFile`, so an API called with a bearer token needs nothing extra. If your API uses cookie [authentication](/web-api/authentication) and is called from browser forms, add `[ValidateAntiForgeryToken]` to the action. The `DisableAntiforgery()` call you may see in other examples is for Minimal APIs only.

</div>

## Safe file handling

Three rules keep uploads from becoming a security hole.

1. Never use the client's file name on disk. It could be `../../Program.cs`. The `Guid` name has no path parts and cannot collide.
2. Store files outside `wwwroot`. `ContentRootPath` is the project folder, so `uploads` is not served as a static file. Nobody can request it directly, and every download goes through your endpoint where you can check access.
3. Check the extension against an allow list. A block list always misses something.

::: warning
An extension check does not prove the file is what it claims. If you open or process uploads, also check the first bytes of the file or scan it. Add `uploads/` to `.gitignore`.
:::

## The download endpoint

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
// at the top of the file
using Microsoft.AspNetCore.StaticFiles;

// in the TodoEndpoints class
static async Task<Results<PhysicalFileHttpResult, NotFound>> DownloadAttachment(
    int id, AppDbContext db, IWebHostEnvironment env)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo?.AttachmentPath is null)
    {
        return TypedResults.NotFound();
    }

    var path = Path.Combine(env.ContentRootPath, "uploads", todo.AttachmentPath);

    if (!new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType))
    {
        contentType = "application/octet-stream";
    }

    return TypedResults.PhysicalFile(path, contentType, todo.AttachmentName);
}
```

`PhysicalFile` streams a file from disk without loading it into memory. Passing `todo.AttachmentName` as the third argument sets the download name, so the user gets `report.pdf` back instead of a `Guid`. `FileExtensionContentTypeProvider` maps an extension to a content type such as `application/pdf`.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
// at the top of the file
using Microsoft.AspNetCore.StaticFiles;

// in the TodosController class
[HttpGet("{id:int}/attachment")]
public async Task<IActionResult> DownloadAttachment(int id)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo?.AttachmentPath is null)
    {
        return NotFound();
    }

    var path = Path.Combine(env.ContentRootPath, "uploads", todo.AttachmentPath);

    if (!new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType))
    {
        contentType = "application/octet-stream";
    }

    return PhysicalFile(path, contentType, todo.AttachmentName);
}
```

`PhysicalFile` streams a file from disk without loading it into memory. Passing `todo.AttachmentName` as the third argument sets the download name, so the user gets `report.pdf` back instead of a `Guid`. `FileExtensionContentTypeProvider` maps an extension to a content type such as `application/pdf`.

</div>

## Trying it out

```http [TodoApi.http]
### Upload an attachment
POST {{TodoApi_HostAddress}}/todos/1/attachment
Content-Type: multipart/form-data; boundary=WebBoundary

--WebBoundary
Content-Disposition: form-data; name="file"; filename="notes.txt"
Content-Type: text/plain

< ./notes.txt
--WebBoundary--

### Download it
GET {{TodoApi_HostAddress}}/todos/1/attachment
```

Create a `notes.txt` next to `TodoApi.http` first. The `< ./notes.txt` line tells the editor to insert that file's contents into the request.

## Size limits

Your 5 MB check runs after ASP.NET Core has received the file. The server has its own limits that run earlier. Kestrel, the built-in web server, rejects request bodies over about 30 MB. To lower that for the whole app:

```csharp [Program.cs (excerpt)]
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});
```

Keep the server limit a little above your largest allowed file, because the multipart wrapper adds some bytes. If you host behind IIS or a reverse proxy such as Nginx, it has its own limit too.

## Learn more

- [Upload files in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads)
- [Prevent Cross-Site Request Forgery attacks in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery)
