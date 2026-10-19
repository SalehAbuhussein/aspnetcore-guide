using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TodoApi.Background;
using TodoApi.Data;
using TodoApi.Models;
using TodoApi.Services;

namespace TodoApi.Endpoints;

public static class TodoEndpoints
{
    public static void MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/todos")
            .WithTags("Todos")
            .RequireAuthorization()
            .RequireRateLimiting("fixed");

        group.MapGet("/", GetAll)
            .AllowAnonymous();
        group.MapGet("/stats", GetStats);
        group.MapGet("/{id:int}", GetById)
            .WithName("GetTodo")
            .WithSummary("Get a todo")
            .WithDescription("Returns one todo item by its ID, or 404 if it does not exist.");
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete)
            .RequireAuthorization("admin");

        group.MapPost("/{id:int}/attachment", UploadAttachment)
            .DisableAntiforgery();
        group.MapGet("/{id:int}/attachment", DownloadAttachment);

        group.MapPost("/{id:int}/remind", Remind);
    }

    // Filters and sorting from the Querying page, paged as on the Pagination page.
    static async Task<Ok<PagedResult<TodoResponse>>> GetAll([AsParameters] TodoQuery query, AppDbContext db)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<Todo> todos = db.Todos;

        if (query.IsDone is not null)
        {
            todos = todos.Where(t => t.IsDone == query.IsDone);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            todos = todos.Where(t => EF.Functions.Like(t.Title, $"%{query.Search}%"));
        }

        todos = query.Sort switch
        {
            "title" => todos.OrderBy(t => t.Title).ThenBy(t => t.Id),
            "-title" => todos.OrderByDescending(t => t.Title).ThenBy(t => t.Id),
            _ => todos.OrderBy(t => t.Id)
        };

        var totalCount = await todos.CountAsync();
        var items = await todos
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TodoResponse(t.Id, t.Title, t.IsDone))
            .ToListAsync();

        return TypedResults.Ok(new PagedResult<TodoResponse>(items, page, pageSize, totalCount));
    }

    static async Task<Ok<TodoStats>> GetStats(AppDbContext db)
    {
        var total = await db.Todos.CountAsync();
        var done = await db.Todos.CountAsync(t => t.IsDone);

        return TypedResults.Ok(new TodoStats(total, done, total - done));
    }

    static async Task<Results<Ok<Todo>, NotFound>> GetById(
        [Range(1, int.MaxValue)] int id, AppDbContext db, ILogger<Todo> logger)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            logger.LogWarning("Todo {TodoId} was not found", id);
            return TypedResults.NotFound();
        }

        logger.LogInformation("Returning todo {TodoId}", id);
        return TypedResults.Ok(todo);
    }

    static async Task<Results<Created<Todo>, Conflict>> Create(CreateTodo input, AppDbContext db)
    {
        if (await db.Todos.AnyAsync(t => t.Title == input.Title))
        {
            return TypedResults.Conflict();
        }

        var todo = new Todo { Title = input.Title };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/todos/{todo.Id}", todo);
    }

    // Problem response from Error handling, CompletedAt from Background tasks,
    // and the "completed" email from Sending email, sent through the queue.
    static async Task<Results<NoContent, ProblemHttpResult>> Update(
        int id, UpdateTodo input, AppDbContext db, EmailQueue queue, IOptions<EmailOptions> options)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return TypedResults.Problem(
                title: "Todo not found",
                detail: $"Todo {id} does not exist.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var justCompleted = !todo.IsDone && input.IsDone;

        todo.Title = input.Title;
        todo.IsDone = input.IsDone;
        todo.CompletedAt = input.IsDone ? todo.CompletedAt ?? DateTime.UtcNow : null;
        await db.SaveChangesAsync();

        if (justCompleted)
        {
            await queue.EnqueueAsync(new EmailMessage(
                options.Value.NotifyTo, "Todo completed", $"\"{todo.Title}\" is done."));
        }

        return TypedResults.NoContent();
    }

    static async Task<Results<NoContent, NotFound>> Delete(int id, AppDbContext db)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return TypedResults.NotFound();
        }

        db.Todos.Remove(todo);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

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

    static async Task<Results<Accepted, NotFound>> Remind(
        int id, AppDbContext db, EmailQueue queue, IOptions<EmailOptions> options)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return TypedResults.NotFound();
        }

        await queue.EnqueueAsync(new EmailMessage(
            options.Value.NotifyTo, "Reminder", $"Don't forget: {todo.Title}"));

        return TypedResults.Accepted($"/todos/{id}");
    }
}
