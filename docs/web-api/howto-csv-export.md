# Export data to CSV

## Introduction

At the end of this guide `GET /todos/export` downloads every todo as a `todos.csv` file that opens in Excel. Rows stream from the database to the client one at a time, so a million todos use no more memory than ten.

Before you start, you need the `/todos` endpoints from [Building CRUD](/web-api/crud).

<ApiStyle />

## 1. Escape values

```csharp [Endpoints/Csv.cs]
namespace TodoApi.Endpoints;

public static class Csv
{
    public static string Escape(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }

        if (value.IndexOfAny([',', '"', '\r', '\n']) >= 0)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
```

A title such as `Buy milk, eggs` contains a comma, which would split it into two columns. CSV fixes that by wrapping the value in double quotes and doubling any quotes inside it.

The first `if` stops CSV injection. Spreadsheet apps run a cell that starts with `=`, `+`, `-`, or `@` as a formula, so a user could store a title that runs code on whoever opens the export. A leading `'` makes it plain text.

## 2. Write the endpoint

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
// at the top of the file
using System.Text;

// in MapTodoEndpoints
group.MapGet("/export", Export);

// in the TodoEndpoints class
static async Task Export(HttpContext http, AppDbContext db, CancellationToken cancellationToken)
{
    http.Response.ContentType = "text/csv; charset=utf-8";
    http.Response.Headers.ContentDisposition = "attachment; filename=\"todos.csv\"";

    await using var writer = new StreamWriter(http.Response.Body, new UTF8Encoding(false));
    await writer.WriteLineAsync("Id,Title,IsDone");

    var todos = db.Todos.AsNoTracking().OrderBy(t => t.Id).AsAsyncEnumerable();

    await foreach (var todo in todos.WithCancellation(cancellationToken))
    {
        await writer.WriteLineAsync($"{todo.Id},{Csv.Escape(todo.Title)},{todo.IsDone}");
    }
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
// at the top of the file
using System.Text;
using TodoApi.Endpoints;

// in the TodosController class
[HttpGet("export")]
public async Task Export(CancellationToken cancellationToken)
{
    Response.ContentType = "text/csv; charset=utf-8";
    Response.Headers.ContentDisposition = "attachment; filename=\"todos.csv\"";

    await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(false));
    await writer.WriteLineAsync("Id,Title,IsDone");

    var todos = db.Todos.AsNoTracking().OrderBy(t => t.Id).AsAsyncEnumerable();

    await foreach (var todo in todos.WithCancellation(cancellationToken))
    {
        await writer.WriteLineAsync($"{todo.Id},{Csv.Escape(todo.Title)},{todo.IsDone}");
    }
}
```

</div>

`AsAsyncEnumerable` returns an `IAsyncEnumerable<Todo>`. `await foreach` reads one row from the database, writes one line, and moves on, so the whole list is never in memory. `ToListAsync` would load every row first. `AsNoTracking` skips change tracking, which you do not need for read-only work.

`Content-Disposition: attachment` tells the browser to save the response as a file named `todos.csv` instead of showing it. `/export` does not clash with `/{id:int}`, because the `int` constraint only matches numbers.

The `StreamWriter` writes straight into the response body. `await using` flushes the last lines when the method ends. `new UTF8Encoding(false)` leaves out the byte order mark. If Excel on Windows shows accented letters wrong, use `new UTF8Encoding(true)` instead.

`cancellationToken` is cancelled when the client disconnects, which stops the database query too.

## 3. Try it

```bash
curl -i http://localhost:5000/todos/export
```

If your `/todos` group requires a signed-in user, add `-H "Authorization: Bearer <token>"`. You get:

```http
HTTP/1.1 200 OK
Content-Type: text/csv; charset=utf-8
Transfer-Encoding: chunked
Content-Disposition: attachment; filename="todos.csv"

Id,Title,IsDone
1,"Buy milk, eggs",False
2,"'=SUM(A1) ""quoted""",False
```

`Transfer-Encoding: chunked` shows the response is streamed: the server did not know the full length when it started sending. Open `http://localhost:5000/todos/export` in a browser and it downloads `todos.csv`.

::: tip
For a filtered export, add query parameters such as `?done=true` and apply `Where` before `AsAsyncEnumerable`, as on [Querying](/web-api/querying).
:::

## Learn more

- [Responses](/web-api/responses)
- [EF Core performance](/web-api/ef-performance)
- [Querying](/web-api/querying)
- [Asynchronous streams in EF Core](https://learn.microsoft.com/ef/core/miscellaneous/async)
