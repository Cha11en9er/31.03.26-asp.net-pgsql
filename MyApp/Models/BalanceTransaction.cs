using System.ComponentModel.DataAnnotations;

namespace MyApp.Models;

public class BalanceTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // "Пополнение" | "Начисление"

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public decimal AmountRub { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

