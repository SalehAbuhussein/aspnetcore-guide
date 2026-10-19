namespace TodoApi.Models;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Todo> Todos { get; set; } = [];
}
