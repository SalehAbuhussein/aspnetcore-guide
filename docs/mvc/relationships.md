# Relationships

## Introduction

Real data links together. A book has many reviews, and a review belongs to one book. This page adds a `Review` entity, loads reviews with their book, and lets visitors post a review from the Details page.

## The Review entity

```csharp [Models/Review.cs]
using System.ComponentModel.DataAnnotations;

namespace BookShelf.Models;

public class Review
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book? Book { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [StringLength(2000)]
    public string Body { get; set; } = "";
}
```

`BookId` is a foreign key, a column that stores the id of the related row in another table. `Book` is a navigation property, a property that lets you move from a review to its book in C#. It is nullable because EF Core only fills it when you ask it to load the book.

Add the other side to `Book`.

```csharp [Models/Book.cs (excerpt)]
public class Book
{
    // Id, Title, Author, Year as before

    public List<Review> Reviews { get; set; } = [];
}
```

And add a `DbSet` to the context.

```csharp [Data/AppDbContext.cs (excerpt)]
public DbSet<Book> Books => Set<Book>();
public DbSet<Review> Reviews => Set<Review>();
```

## One-to-many by convention

You do not need any configuration for this relationship. EF Core sees `List<Review> Reviews` on `Book`, `Book` on `Review`, and a property named `BookId`, and works out that one book has many reviews. This is called a convention: a rule EF Core follows when you name things the expected way.

The foreign key is a non-nullable `int`, so every review must belong to a book. EF Core also sets up cascade delete, which means deleting a book deletes its reviews.

If your names do not follow the convention, configure the relationship in the `OnModelCreating` method you added on the [Migrations](/mvc/migrations) page:

```csharp [Data/AppDbContext.cs (excerpt)]
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // The HasData call from the Migrations page stays here.

    modelBuilder.Entity<Review>()
        .HasOne(r => r.Book)
        .WithMany(b => b.Reviews)
        .HasForeignKey(r => r.BookId);
}
```

## Creating the migration

```bash
dotnet ef migrations add AddReviews
dotnet ef database update
```

The migration creates a `Reviews` table with a `BookId` column, an index on it, and a foreign key constraint that points at `Books`. See [Migrations](/mvc/migrations).

## Loading related data with Include

```csharp [Controllers/BooksController.cs (excerpt)]
// GET: /Books/Details/5
public async Task<IActionResult> Details(int id)
{
    var book = await db.Books
        .Include(b => b.Reviews)
        .FirstOrDefaultAsync(b => b.Id == id);

    if (book is null) return NotFound();
    return View(book);
}
```

EF Core does not load related rows unless you ask. Without `Include`, `book.Reviews` would be an empty list. `Include(b => b.Reviews)` tells EF Core to load the reviews in the same query. `FindAsync` cannot take an `Include`, so the action uses `FirstOrDefaultAsync` instead.

## Adding a review from the Details page

Add a POST action that saves a review for one book.

```csharp [Controllers/BooksController.cs (excerpt)]
// POST: /Books/AddReview/5
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AddReview(int id, [Bind("Rating,Body")] Review review)
{
    if (!await db.Books.AnyAsync(b => b.Id == id)) return NotFound();

    if (!ModelState.IsValid)
    {
        TempData["Message"] = "A review needs a rating from 1 to 5 and some text.";
        return RedirectToAction(nameof(Details), new { id });
    }

    review.BookId = id;
    db.Reviews.Add(review);
    await db.SaveChangesAsync();
    return RedirectToAction(nameof(Details), new { id });
}
```

`[Bind("Rating,Body")]` tells model binding to fill only those two properties, so a visitor cannot post their own `BookId` or `Id`. The action takes the book id from the URL instead. On success it redirects back to the Details page.

Show the reviews and the form in the view.

```razor [Views/Books/Details.cshtml]
@model BookShelf.Models.Book

<h1>@Model.Title</h1>
<p>@Model.Author, @Model.Year</p>
<a asp-action="Edit" asp-route-id="@Model.Id">Edit</a>
<a asp-action="Index">Back to list</a>

@if (TempData["Message"] is string message)
{
    <p>@message</p>
}

<h2>Reviews (@Model.Reviews.Count)</h2>
@foreach (var review in Model.Reviews)
{
    <p>@review.Rating/5: @review.Body</p>
}

<form asp-action="AddReview" asp-route-id="@Model.Id" method="post">
    <label for="Rating">Rating</label>
    <input id="Rating" name="Rating" type="number" min="1" max="5" value="5" />
    <label for="Body">Review</label>
    <textarea id="Body" name="Body"></textarea>
    <button type="submit">Post review</button>
</form>
```

The view's model is a `Book`, so the review fields use plain `name` attributes that match the `Review` properties. Model binding matches them by name. The form tag helper still adds the antiforgery token.

## Many-to-many

Some links go both ways. A book can have many tags, and a tag can be on many books.

```csharp [Models/Tag.cs]
namespace BookShelf.Models;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Book> Books { get; set; } = [];
}
```

Add `public List<Tag> Tags { get; set; } = [];` to `Book` and `public DbSet<Tag> Tags => Set<Tag>();` to `AppDbContext`, then add a migration. EF Core sees a list on each side and creates a join table named `BookTag` for you. The lists are called skip navigations, because they skip over the join table. You never write a class for it.

```csharp
var book = await db.Books.Include(b => b.Tags).FirstAsync(b => b.Id == 1);
var tag = await db.Tags.FirstAsync(t => t.Name == "Fantasy");
book.Tags.Add(tag);
await db.SaveChangesAsync();
```

Adding a tag to the list and saving inserts a row in the join table. When the join row needs extra data, such as the date the tag was added, create an explicit `BookTag` entity instead.

## Learn more

- [Introduction to relationships](https://learn.microsoft.com/ef/core/modeling/relationships)
- [Eager loading of related data](https://learn.microsoft.com/ef/core/querying/related-data/eager)
