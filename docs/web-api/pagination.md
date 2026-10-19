# Pagination

## Introduction

Pagination splits a long list into pages, so `GET /todos` returns 20 rows at a time instead of every row in the table. This page adds `?page=1&pageSize=20` to the todo list and wraps the result in a response that tells the client how many pages there are.

<ApiStyle />

## The query parameters

```csharp [Models/PageQuery.cs]
namespace TodoApi.Models;

public record PageQuery(int Page = 1, int PageSize = 20);
```

A query parameter is a value in the URL after the `?`, such as `page=2`. This record groups the two paging values in one type. The default values apply when the client leaves a parameter out, so `GET /todos` behaves like `GET /todos?page=1&pageSize=20`.

## The response shape

```csharp [Models/PagedResult.cs]
namespace TodoApi.Models;

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

`PagedResult<T>` is generic, meaning `T` is a placeholder for any type. You can reuse it for todos, projects, or anything else. `TotalCount` is the number of rows across all pages, and `TotalPages` is worked out from it. Clients use both to draw "page 2 of 7" and to know when to stop asking for more.

## The paged endpoint

<div class="only-minimal">

Replace the `GetAll` from [Your first API](/web-api/first-app) in `Endpoints/TodoEndpoints.cs`. The [CRUD](/web-api/crud) page shows the same unpaged `GetAll`, so keep this paged version when you reach it. The route stays `group.MapGet("/", GetAll);`. The handler reads from the `AppDbContext` you set up in [Your first API](/web-api/first-app), so the file needs `using Microsoft.EntityFrameworkCore;` and `using TodoApi.Data;` at the top.

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
static async Task<Ok<PagedResult<Todo>>> GetAll([AsParameters] PageQuery query, AppDbContext db)
{
    var page = Math.Max(query.Page, 1);
    var pageSize = Math.Clamp(query.PageSize, 1, 100);

    var totalCount = await db.Todos.CountAsync();

    var items = await db.Todos
        .OrderBy(t => t.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return TypedResults.Ok(new PagedResult<Todo>(items, page, pageSize, totalCount));
}
```

`[AsParameters]` tells ASP.NET Core to fill each property of `PageQuery` from the request. Here `Page` and `PageSize` come from the query string. Without it, ASP.NET Core would try to read `PageQuery` from the JSON body.

</div>

<div class="only-controllers">

Replace the `GetAll` from [Your first API](/web-api/first-app) in `Controllers/TodosController.cs`. The [CRUD](/web-api/crud) page shows the same unpaged `GetAll`, so keep this paged version when you reach it. The route stays `[HttpGet]`. The action reads from the `AppDbContext` the controller receives through its constructor, as in [Your first API](/web-api/first-app), so the file needs `using Microsoft.EntityFrameworkCore;` and `using TodoApi.Data;` at the top.

```csharp [Controllers/TodosController.cs (excerpt)]
[HttpGet]
public async Task<ActionResult<PagedResult<Todo>>> GetAll([FromQuery] PageQuery query)
{
    var page = Math.Max(query.Page, 1);
    var pageSize = Math.Clamp(query.PageSize, 1, 100);

    var totalCount = await db.Todos.CountAsync();

    var items = await db.Todos
        .OrderBy(t => t.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return new PagedResult<Todo>(items, page, pageSize, totalCount);
}
```

`[FromQuery]` tells ASP.NET Core to fill each property of `PageQuery` from the query string. Without it, the `[ApiController]` attribute would try to read `PageQuery` from the JSON body.

</div>

`Skip` jumps over the rows on earlier pages and `Take` returns the next `pageSize` rows. On page 3 with a page size of 20, that skips 40 rows and takes 20. EF Core turns this into `LIMIT` and `OFFSET` in the SQL it sends to SQLite.

## Clamping the page size

```csharp
var pageSize = Math.Clamp(query.PageSize, 1, 100);
```

`Math.Clamp` forces a number into a range. A client that asks for `pageSize=100000` gets 100 rows, and one that asks for `pageSize=0` gets 1. Without a cap, one request could load your whole table into memory. `Math.Max(query.Page, 1)` does the same for pages, so `page=-5` is treated as page 1.

You could reject bad values with a 400 response instead, using `[Range]` from [Validation](/web-api/validation). Clamping is friendlier for paging, because the client still gets a sensible answer.

## Always order before paging

```csharp
.OrderBy(t => t.Id)
.Skip((page - 1) * pageSize)
```

A database does not promise to return rows in any order unless you ask for one. Without `OrderBy`, two requests for page 1 and page 2 can overlap or skip rows, because the database may pick a different order each time. Sort on a column with unique values, such as `Id`, so every row has one fixed position.

EF Core logs a warning when you call `Skip` or `Take` without `OrderBy`. Treat it as a bug.

::: tip
If you sort on a column that can repeat, such as `Title`, add the `Id` as a tie breaker: `.OrderBy(t => t.Title).ThenBy(t => t.Id)`.
:::

## Trying it out

```http [TodoApi.http]
### First page
GET {{TodoApi_HostAddress}}/todos

### Second page of 5
GET {{TodoApi_HostAddress}}/todos?page=2&pageSize=5
```

The response now looks like this:

```json
{
  "items": [
    { "id": 6, "title": "Buy milk", "isDone": false }
  ],
  "page": 2,
  "pageSize": 5,
  "totalCount": 6,
  "totalPages": 2
}
```

`TotalPages` is a read-only property, and System.Text.Json writes it to the response like any other property.

## Counting has a cost

`CountAsync` runs a second query on every request. On a table with a few thousand rows that is cheap. On very large tables, consider keyset paging, where the client sends the last `Id` it saw and you query `Where(t => t.Id > lastId).Take(pageSize)`. It skips the count and stays fast on deep pages, but clients can no longer jump to page 50.

The [Querying](/web-api/querying) page adds filters and sorting. Apply them before `CountAsync` so the total matches the filtered list.

## Learn more

- [Pagination in EF Core](https://learn.microsoft.com/ef/core/querying/pagination)
- [Parameter binding in Minimal API apps](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/parameter-binding)
