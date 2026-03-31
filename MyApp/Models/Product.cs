using System.ComponentModel.DataAnnotations;

namespace MyApp.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(1200)]
    public string Description { get; set; } = string.Empty;

    [Range(0, 1000000000)]
    public decimal PriceRub { get; set; }
}

