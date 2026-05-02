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

    /// <summary>Код подтверждения email (6 цифр).</summary>
    [MaxLength(16)]
    public string? EmailConfirmationCode { get; set; }

    public DateTime? EmailConfirmationCodeExpiresAt { get; set; }
    public DateTime? EmailCodeVerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
