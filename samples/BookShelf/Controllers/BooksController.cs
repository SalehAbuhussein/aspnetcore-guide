using System.Text.Json;
using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BookShelf.Controllers;

[Authorize]
public class BooksController(
    AppDbContext db,
    IWebHostEnvironment env,
    EmailQueue emailQueue,
    IOptions<SmtpOptions> smtp,
    ILogger<BooksController> logger) : Controller
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxCoverBytes = 2 * 1024 * 1024; // 2 MB

    // GET: /Books?search=dune&page=2
    [AllowAnonymous]
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

    // GET: /Books/Details/5
    public async Task<IActionResult> Details(int id)
    {
        logger.LogInformation("Loading book {BookId}", id);

        var book = await db.Books
            .Include(b => b.Reviews)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (book is null)
        {
            logger.LogWarning("Book {BookId} was not found", id);
            return NotFound();
        }

        // Recently viewed books, kept in the session (see the Sessions page).
        var json = HttpContext.Session.GetString("RecentBooks");
        List<int> recent = json is null ? [] : JsonSerializer.Deserialize<List<int>>(json)!;

        recent.Remove(id);
        recent.Insert(0, id);
        if (recent.Count > 5) recent.RemoveAt(5);

        HttpContext.Session.SetString("RecentBooks", JsonSerializer.Serialize(recent));

        ViewData["Recent"] = await db.Books
            .Where(b => recent.Contains(b.Id) && b.Id != id)
            .ToListAsync();

        return View(book);
    }

    // GET: /Books/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Books/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("create")]
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

        logger.LogInformation("User {UserName} created book {BookId} titled {Title}",
            User.Identity?.Name, book.Id, book.Title);

        await emailQueue.EnqueueAsync(new EmailMessage(
            smtp.Value.NotifyAddress,
            $"New book: {book.Title}",
            $"{book.Title} by {book.Author} ({book.Year}) was added to BookShelf."));

        TempData["Message"] = $"Added \"{book.Title}\".";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Books/Edit/5
    [Authorize(Policy = "CanEditBooks")]
    public async Task<IActionResult> Edit(int id)
    {
        var book = await db.Books.FindAsync(id);
        if (book is null) return NotFound();
        return View(book);
    }

    // POST: /Books/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CanEditBooks")]
    public async Task<IActionResult> Edit(int id, Book book)
    {
        if (id != book.Id) return BadRequest();
        if (!ModelState.IsValid) return View(book);

        db.Books.Update(book);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: /Books/Delete/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var book = await db.Books.FindAsync(id);
        if (book is null) return NotFound();
        return View(book);
    }

    // POST: /Books/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var book = await db.Books.FindAsync(id);
        if (book is not null)
        {
            db.Books.Remove(book);
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

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
}
