using System.ComponentModel.DataAnnotations;

namespace MyApp.Models;

public class LegalEntityProfile
{
    public Guid UserId { get; set; }

    [MaxLength(500)]
    public string CompanyFullName { get; set; } = string.Empty;

    [MaxLength(160)]
    public string CompanyShortName { get; set; } = string.Empty;

    /// <summary>ИНН юридического лица (10 цифр).</summary>
    [MaxLength(10)]
    public string Inn { get; set; } = string.Empty;

    /// <summary>ОГРН (13 цифр).</summary>
    [MaxLength(13)]
    public string Ogrn { get; set; } = string.Empty;

    /// <summary>КПП (9 цифр).</summary>
    [MaxLength(9)]
    public string Kpp { get; set; } = string.Empty;

    [MaxLength(200)]
    public string DirectorFullName { get; set; } = string.Empty;

    public DateOnly DirectorBirthDate { get; set; }

    [MaxLength(255)]
    public string DocumentFileName { get; set; } = string.Empty;

    public byte[] DocumentContent { get; set; } = Array.Empty<byte>();

    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
