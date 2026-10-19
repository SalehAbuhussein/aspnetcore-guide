using TodoApi.Models;

namespace TodoApi.Data;

public static class SampleData
{
    public static List<Todo> Todos() =>
    [
        new() { Title = "Buy milk" },
        new() { Title = "Walk the dog", IsDone = true },
        new() { Title = "Read the EF Core docs", ProjectId = 1 },
        new() { Title = "Write tests", ProjectId = 1 }
    ];
}
