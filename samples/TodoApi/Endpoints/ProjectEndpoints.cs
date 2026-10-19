using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/projects")
            .WithTags("Projects");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create);
        group.MapGet("/{id:int}/todos", GetTodos);
        group.MapPost("/{id:int}/todos", AddTodo);
    }

    static async Task<Ok<List<ProjectSummary>>> GetAll(AppDbContext db)
    {
        var projects = await db.Projects
            .OrderBy(p => p.Name)
            .Select(p => new ProjectSummary(p.Id, p.Name, p.Todos.Count))
            .ToListAsync();

        return TypedResults.Ok(projects);
    }

    static async Task<Results<Ok<ProjectDetails>, NotFound>> GetById(int id, AppDbContext db)
    {
        var project = await db.Projects
            .Include(p => p.Todos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null)
        {
            return TypedResults.NotFound();
        }

        var todos = project.Todos
            .Select(t => new TodoItem(t.Id, t.Title, t.IsDone))
            .ToList();

        return TypedResults.Ok(new ProjectDetails(project.Id, project.Name, todos));
    }

    static async Task<Created<ProjectSummary>> Create(CreateProject input, AppDbContext db)
    {
        var project = new Project { Name = input.Name };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/projects/{project.Id}",
            new ProjectSummary(project.Id, project.Name, 0));
    }

    static async Task<Results<Ok<List<TodoItem>>, NotFound>> GetTodos(int id, AppDbContext db)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == id))
        {
            return TypedResults.NotFound();
        }

        var todos = await db.Todos
            .Where(t => t.ProjectId == id)
            .Select(t => new TodoItem(t.Id, t.Title, t.IsDone))
            .ToListAsync();

        return TypedResults.Ok(todos);
    }

    static async Task<Results<Created<TodoItem>, NotFound>> AddTodo(int id, CreateTodo input, AppDbContext db)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == id))
        {
            return TypedResults.NotFound();
        }

        var todo = new Todo { Title = input.Title, ProjectId = id };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/todos/{todo.Id}", new TodoItem(todo.Id, todo.Title, todo.IsDone));
    }
}
