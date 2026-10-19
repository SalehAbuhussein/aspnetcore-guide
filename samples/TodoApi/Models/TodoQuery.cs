namespace TodoApi.Models;

// Filters from the Querying page plus Page and PageSize from the Pagination page.
public record TodoQuery(
    bool? IsDone = null,
    string? Search = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = 20);

public record TodoResponse(int Id, string Title, bool IsDone);

public record TodoStats(int Total, int Done, int Open);
