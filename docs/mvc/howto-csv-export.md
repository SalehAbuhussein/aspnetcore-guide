# Export data to CSV

## Introduction

At the end of this guide, the BookShelf book list has a Download CSV button that saves every book to a `books.csv` file you can open in Excel or Google Sheets. Before you start, you need `BooksController` and `AppDbContext` from [Database](/mvc/database) and [CRUD](/mvc/crud).

## 1. Add the export action

```csharp [Controllers/BooksController.cs (excerpt)]
using System.Text;

// GET: /Books/Export
public async Task<IActionResult> Export()
{
    var books = await db.Books.AsNoTracking().OrderBy(b => b.Title).ToListAsync();

    var csv = new StringBuilder();
    csv.AppendLine("Id,Title,Author,Year");
    foreach (var b in books)
    {
        csv.AppendLine($"{b.Id},{Escape(b.Title)},{Escape(b.Author)},{b.Year}");
    }

    var bytes = Encoding.UTF8.GetPreamble()
        .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
        .ToArray();
    return File(bytes, "text/csv", "books.csv");
}
```

`AsNoTracking` skips change tracking, since the books are only read. The first line is the header row.

`File(bytes, "text/csv", "books.csv")` returns the bytes with the CSV content type. The third argument adds a `Content-Disposition: attachment` header, which tells the browser to download the file under that name instead of showing it.

`GetPreamble` adds the UTF-8 byte order mark, three bytes at the start of the file. Excel needs it to read accented characters such as "é" correctly. Other programs ignore it.

## 2. Escape the values

```csharp [Controllers/BooksController.cs (excerpt)]
private static string Escape(string value)
{
    if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
    {
        return value;
    }
    return "\"" + value.Replace("\"", "\"\"") + "\"";
}
```

In CSV, a comma starts a new column and a newline starts a new row. A title like `Hello, World` would split into two columns. The rule from RFC 4180, the CSV standard, is to wrap such a value in double quotes and to double any quote inside it.

| Title in the database | In the file |
| --- | --- |
| `Dune` | `Dune` |
| `Hello, World` | `"Hello, World"` |
| `The "Best" Book` | `"The ""Best"" Book"` |

Numbers like `Id` and `Year` never contain these characters, so they skip `Escape`.

::: warning
If people outside your team open the file in Excel, a value that starts with `=`, `+`, `-`, or `@` runs as a formula. For user-entered text, prefix such values with a single quote `'` to stop that.
:::

## 3. Add the button

```razor [Views/Books/Index.cshtml (excerpt)]
<a asp-action="Export" class="btn btn-outline-secondary">Download CSV</a>
```

This is a plain link, so a GET request is fine. Exporting does not change any data. If `BooksController` has `[Authorize]` on the class, only signed-in users can export, which is usually what you want.

## 4. Know when to use CsvHelper

```bash
dotnet add package CsvHelper
```

The hand-written version is fine for a few thousand rows and simple columns. Switch to CsvHelper, the most used CSV library for .NET, when you need any of these:

- Large tables. CsvHelper can write straight to the response stream, so the whole file never sits in memory.
- Dates and decimals in a fixed culture, or many columns mapped from classes.
- Reading CSV files that users upload, which has far more edge cases than writing.

## 5. Try it

Add a book whose title is `Hello, "World"`. Run the app, open `https://localhost:<port>/Books`, and click Download CSV. Your browser saves `books.csv`. Open it in a text editor.

```text [books.csv]
Id,Title,Author,Year
1,"Hello, ""World""",Ann,2020
```

Open the same file in Excel or Google Sheets. The title sits in one cell as `Hello, "World"`.

From the command line, check the headers. If the controller requires login, run this against an action that allows anonymous access, or copy your browser's cookie.

```bash
curl -k -i https://localhost:<port>/Books/Export
```

The response has `Content-Type: text/csv` and `Content-Disposition: attachment; filename=books.csv`.

## Learn more

- [Controllers](/mvc/controllers)
- [Querying](/mvc/querying)
- [Controller action return types](https://learn.microsoft.com/aspnet/core/web-api/action-return-types)
