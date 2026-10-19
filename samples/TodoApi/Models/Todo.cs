namespace TodoApi.Models;

public class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
    public DateTime? DueDate { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public List<Tag> Tags { get; set; } = [];
    public string? AttachmentPath { get; set; }
    public string? AttachmentName { get; set; }
    public DateTime? CompletedAt { get; set; }
}
