namespace MyApp.Models;

public class UserAccount
{
    public Guid UserId { get; set; }
    public decimal BalanceRub { get; set; }
    public decimal DebtRub { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

