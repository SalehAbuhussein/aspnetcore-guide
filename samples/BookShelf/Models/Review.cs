using System.ComponentModel.DataAnnotations;

namespace BookShelf.Models;

public class Review
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book? Book { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [StringLength(2000)]
    public string Body { get; set; } = "";
}
