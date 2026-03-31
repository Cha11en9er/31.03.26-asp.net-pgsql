using System.ComponentModel.DataAnnotations;

namespace MyApp.Models;

public class Contract
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int ProductId { get; set; }

    [MaxLength(50)]
    public string ContractNumber { get; set; } = string.Empty;

    public decimal AmountRub { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;

    public User User { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

