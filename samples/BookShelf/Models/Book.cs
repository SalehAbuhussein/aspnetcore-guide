using System.ComponentModel.DataAnnotations;

namespace BookShelf.Models;

public class Book
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string Author { get; set; } = "";

    [Range(1450, 2100, ErrorMessage = "Enter a year between 1450 and 2100.")]
    public int Year { get; set; }

    public string? Isbn { get; set; }

    public List<Review> Reviews { get; set; } = [];

    public List<Tag> Tags { get; set; } = [];

    public string? CoverPath { get; set; }
}
