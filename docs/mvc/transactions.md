# Transactions and concurrency

## Introduction

Two problems show up once real people use your app. Some operations touch several rows and must succeed or fail as one unit, and two people can edit the same book at the same time and overwrite each other. This page covers transactions for the first problem and concurrency tokens for the second. A small app with one editor can live without concurrency checks, but anything with a team behind it needs them.

## SaveChanges is already a transaction

```csharp
db.Books.Add(new Book { Title = "Dune", Author = "Frank Herbert", Year = 1965 });
db.Books.Remove(oldBook);
await db.SaveChangesAsync();
```

A transaction is a group of database changes that either all happen or none happen. EF Core wraps every `SaveChangesAsync` call in one. If the delete fails, the insert is rolled back too. For most actions in BookShelf, this is all you need, so do not add transactions where one `SaveChangesAsync` call covers the work.

## Transactions across several calls

You need an explicit transaction when the work spans more than one call to the database. A common case is mixing `SaveChangesAsync` with `ExecuteUpdateAsync` or `ExecuteDeleteAsync`, which run their SQL right away.

This action merges a duplicate book into another one. It moves the reviews, then deletes the duplicate.

```csharp [Controllers/BooksController.cs (excerpt)]
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Merge(int keepId, int duplicateId)
{
    await using var transaction = await db.Database.BeginTransactionAsync();

    await db.Set<Review>()
        .Where(r => r.BookId == duplicateId)
        .ExecuteUpdateAsync(s => s.SetProperty(r => r.BookId, keepId));

    await db.Books
        .Where(b => b.Id == duplicateId)
        .ExecuteDeleteAsync();

    await transaction.CommitAsync();

    return RedirectToAction(nameof(Details), new { id = keepId });
}
```

`BeginTransactionAsync` opens a transaction, and every command after it joins in. `CommitAsync` makes the changes permanent. If an exception is thrown before the commit, `await using` disposes the transaction, which rolls everything back. You never end up with reviews pointing at a deleted book.

If your context has a `Reviews` property, use `db.Reviews` in place of `db.Set<Review>()`.

::: tip
When you move to SQL Server with `EnableRetryOnFailure`, a manual transaction throws an error unless you wrap it in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`. The retry logic needs to replay the whole unit.
:::

## The lost update problem

Alice opens the edit form for Dune. Bob opens it too. Bob fixes the year and saves. Alice fixes a typo in the title and saves. Alice's form still had the old year, so her save puts it back. Bob's change is gone and nobody gets an error.

A concurrency token stops this. It is a column that EF Core checks in the `WHERE` clause of every `UPDATE`. If the value changed since the form was loaded, no row matches, and EF Core throws `DbUpdateConcurrencyException`.

## Adding a version to Book

SQL Server has a `rowversion` column type that changes on every update. SQLite has nothing like it, so for BookShelf you add a `Guid` property and change it yourself on each save.

```csharp [Models/Book.cs (excerpt)]
using System.ComponentModel.DataAnnotations;

public class Book
{
    // ... existing properties ...

    [ConcurrencyCheck]
    public Guid Version { get; set; }
}
```

`[ConcurrencyCheck]` tells EF Core to include `Version` in the `WHERE` clause of updates and deletes. Now give it a new value whenever a book is saved.

```csharp [Data/AppDbContext.cs (excerpt)]
public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    foreach (var entry in ChangeTracker.Entries<Book>())
    {
        if (entry.State is EntityState.Added or EntityState.Modified)
        {
            entry.Entity.Version = Guid.NewGuid();
        }
    }

    return base.SaveChangesAsync(cancellationToken);
}
```

EF Core compares against the original value, the one loaded from the form, and writes the new one. Doing it in the context means no controller can forget. Add a migration for the new column.

```bash
dotnet ef migrations add AddBookVersion
dotnet ef database update
```

### The SQL Server variant

```csharp [Models/Book.cs (excerpt)]
[Timestamp]
public byte[] RowVersion { get; set; } = [];
```

On SQL Server, `[Timestamp]` maps to `rowversion` and the database updates it for you, so you skip the `SaveChangesAsync` override. Everything else on this page works the same with `RowVersion` in place of `Version`.

## Sending the version with the form

```razor [Views/Books/Edit.cshtml (excerpt)]
<input type="hidden" asp-for="Id" />
<input type="hidden" asp-for="Version" />
```

The form must carry the version the user started from. Without the hidden field, every save would send an empty `Guid` and fail.

## Handling the conflict in Edit

```csharp [Controllers/BooksController.cs (excerpt)]
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(int id, Book book)
{
    if (id != book.Id) return BadRequest();
    if (!ModelState.IsValid) return View(book);

    db.Books.Update(book);

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        var entry = ex.Entries.Single();
        var databaseValues = await entry.GetDatabaseValuesAsync();

        if (databaseValues is null)
        {
            ModelState.AddModelError(string.Empty, "Someone else deleted this book.");
            return View(book);
        }

        var current = (Book)databaseValues.ToObject();

        ModelState.AddModelError(string.Empty,
            $"Someone else changed this book while you were editing. " +
            $"It now reads \"{current.Title}\" by {current.Author}, {current.Year}. " +
            "Save again to keep your version.");

        book.Version = current.Version;
        ModelState.Remove(nameof(Book.Version));
        return View(book);
    }

    return RedirectToAction(nameof(Index));
}
```

`ex.Entries` holds the entities that failed to save. `GetDatabaseValuesAsync` reads the row as it is now, or returns `null` when the row was deleted. The error goes into `ModelState`, so the validation summary at the top of the form shows it.

The action then copies the current version into the model and removes the old value from `ModelState`. Tag helpers prefer `ModelState` values over the model, so without `Remove` the hidden field would keep the stale version. If the user saves again, their edit wins on purpose, because they have now seen the other change.

## Learn more

- [Transactions in EF Core](https://learn.microsoft.com/ef/core/saving/transactions)
- [Handling concurrency conflicts](https://learn.microsoft.com/ef/core/saving/concurrency)
