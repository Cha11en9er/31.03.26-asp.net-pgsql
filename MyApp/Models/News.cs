using System.ComponentModel.DataAnnotations;

namespace MyApp.Models;

public class News
{
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}
