namespace MyApp.DTOs;

public class LegalVerificationFormDto
{
    public string? CompanyFullName { get; set; }
    public string? CompanyShortName { get; set; }
    public string? Inn { get; set; }
    public string? Ogrn { get; set; }
    public string? Kpp { get; set; }
    public string? DirectorFullName { get; set; }
    public DateOnly DirectorBirthDate { get; set; }
}
