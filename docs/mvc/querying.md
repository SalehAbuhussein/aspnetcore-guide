# Querying

## Introduction

This page builds a better book list. You will filter and sort with LINQ, add a search box, load only the columns the page needs, and split long lists into pages. It uses `AppDbContext` from the [Database](/mvc/database) page and the reviews from [Relationships](/mvc/relationships).

## Filtering and sorting

```csharp
var recent = await db.Books
    .Where(b => b.Year >= 2000)
    .OrderByDescending(b => b.Year)
    .ThenBy(b => b.Title)
    .ToListAsync();
```

`Where` keeps rows that match a condition. `OrderBy` and `OrderByDescending` sort, and `ThenBy` breaks ties. EF Core turns the whole chain into one SQL query and runs it when you call `ToListAsync`. Until then, the query is only a description, so you can add to it step by step.

## A search box

```csharp [Controllers/BooksController.cs (excerpt)]
// GET: /Books?search=dune
public async Task<IActionResult> Index(string? search)
{
    var query = db.Books.AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(b =>
            EF.Functions.Like(b.Title, $"%{search}%") ||
            EF.Functions.Like(b.Author, $"%{search}%"));
    }

    var books = await query.OrderBy(b => b.Title).ToListAsync();
    return View(books);
}
```

The `search` parameter comes from the query string, the part of the URL after `?`. `EF.Functions.Like` becomes a SQL `LIKE`, and `%` matches any text. In SQLite, `LIKE` ignores case for English letters, so "dune" finds "Dune". EF Core sends `search` as a parameter, never as raw SQL, so this is safe from SQL injection.

```razor [Views/Books/Index.cshtml (excerpt)]
<form method="get">
    <input name="search" value="@Context.Request.Query["search"]" placeholder="Title or author" />
    <button type="submit">Search</button>
</form>
```

A form with `method="get"` puts its fields in the URL, so `/Books?search=dune` can be bookmarked and shared.

## Selecting into a view model

The list page shows a title, an author, and a review count. It does not need every column, and it should not load every review only to count them. Select only what you need into a view model, a class shaped for one page.

```csharp [Models/BookListViewModel.cs]
namespace BookShelf.Models;

public record BookRow(int Id, string Title, string Author, int ReviewCount);

public class BookListViewModel
{
    public List<BookRow> Books { get; set; } = [];
    public string? Search { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
```

This replaces the `BookListViewModel` from the [Models](/mvc/models) page. A `record` is a short way to write a class that only holds data.

```csharp
var rows = await db.Books
    .OrderBy(b => b.Title)
    .Select(b => new BookRow(b.Id, b.Title, b.Author, b.Reviews.Count))
    .ToListAsync();
```

`Select` changes the shape of each result. EF Core reads the four values in SQL and counts the reviews with a subquery. No `Include` is needed, and no `Book` objects are created.

## Count and Any

```csharp
int total = await db.Books.CountAsync();
int modern = await db.Books.CountAsync(b => b.Year >= 2000);
bool hasReviews = await db.Reviews.AnyAsync(r => r.BookId == id);
```

`CountAsync` returns how many rows match. `AnyAsync` returns `true` if at least one row matches. Use `AnyAsync` when you only need yes or no, because the database can stop at the first match.

## Paging with Skip and Take

Put it all together in the `Index` action.

```csharp [Controllers/BooksController.cs (excerpt)]
// GET: /Books?search=dune&page=2
public async Task<IActionResult> Index(string? search, int page = 1)
{
    const int pageSize = 10;
    var query = db.Books.AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(b =>
            EF.Functions.Like(b.Title, $"%{search}%") ||
            EF.Functions.Like(b.Author, $"%{search}%"));
    }

    var total = await query.CountAsync();
    var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
    page = Math.Clamp(page, 1, totalPages);

    var rows = await query
        .OrderBy(b => b.Title)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(b => new BookRow(b.Id, b.Title, b.Author, b.Reviews.Count))
        .ToListAsync();

    return View(new BookListViewModel
    {
        Books = rows,
        Search = search,
        TotalCount = total,
        Page = page,
        TotalPages = totalPages
    });
}
```

`Skip` jumps over the rows on earlier pages and `Take` returns at most one page of rows. Always sort before you page. Without `OrderBy`, the database can return rows in any order, and a book could show up on two pages. `Math.Clamp` keeps the page number between 1 and the last page.

## The pager in the view

```razor [Views/Books/Index.cshtml]
@model BookShelf.Models.BookListViewModel

<h1>Books</h1>
<a asp-action="Create">Add a book</a>

<form method="get">
    <input name="search" value="@Model.Search" placeholder="Title or author" />
    <button type="submit">Search</button>
</form>

<p>@Model.TotalCount books found.</p>

<table>
    @foreach (var book in Model.Books)
    {
        <tr>
            <td><a asp-action="Details" asp-route-id="@book.Id">@book.Title</a></td>
            <td>@book.Author</td>
            <td>@book.ReviewCount reviews</td>
        </tr>
    }
</table>

<nav>
    @if (Model.Page > 1)
    {
        <a asp-action="Index" asp-route-search="@Model.Search" asp-route-page="@(Model.Page - 1)">Previous</a>
    }
    <span>Page @Model.Page of @Model.TotalPages</span>
    @if (Model.Page < Model.TotalPages)
    {
        <a asp-action="Index" asp-route-search="@Model.Search" asp-route-page="@(Model.Page + 1)">Next</a>
    }
</nav>
```

`asp-route-search` and `asp-route-page` add query string values to the link, so the search term carries over from page to page. When `Search` is `null`, the tag helper leaves it out of the URL.

::: tip
Skip and Take get slower on very deep pages, because the database still walks past every skipped row. For lists with many thousands of rows, look at keyset paging on the [EF Core performance](/mvc/ef-performance) page.
:::

## Learn more

- [Querying data with EF Core](https://learn.microsoft.com/ef/core/querying/)
- [Pagination in EF Core](https://learn.microsoft.com/ef/core/querying/pagination)
