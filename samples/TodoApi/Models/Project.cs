namespace TodoApi.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Todo> Todos { get; set; } = [];
}
