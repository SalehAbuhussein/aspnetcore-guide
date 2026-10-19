using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models;

public record CreateProject(
    [Required, StringLength(100)] string Name);

public record ProjectSummary(int Id, string Name, int TodoCount);

public record ProjectDetails(int Id, string Name, List<TodoItem> Todos);

public record TodoItem(int Id, string Title, bool IsDone);
