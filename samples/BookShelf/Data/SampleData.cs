using BookShelf.Models;

namespace BookShelf.Data;

public static class SampleData
{
    public static Book Book() => new()
    {
        Title = "Project Hail Mary",
        Author = "Andy Weir",
        Year = 2021,
        Reviews =
        [
            new Review { Rating = 5, Body = "Could not put it down." },
            new Review { Rating = 4, Body = "Great science, fun narrator." }
        ]
    };
}
