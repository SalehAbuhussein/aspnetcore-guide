using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models;

public record CreateTodo(
    [Required, StringLength(100)] string Title);

public class UpdateTodo
{
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = "";

    public bool IsDone { get; set; }
}
