using System.ComponentModel.DataAnnotations;

namespace MyApp.Models;

public class User
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsEmailConfirmed { get; set; }

    /// <summary>Одноразовый токен подтверждения email (до подтверждения).</summary>
    [MaxLength(128)]
    public string? EmailConfirmationToken { get; set; }

    public DateTime? EmailConfirmationTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
