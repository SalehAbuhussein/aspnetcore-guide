namespace BookShelf.Models;

public record BookRow(int Id, string Title, string Author, int ReviewCount);

public class BookListViewModel
{
    public List<BookRow> Books { get; set; } = [];
    public string? Search { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
