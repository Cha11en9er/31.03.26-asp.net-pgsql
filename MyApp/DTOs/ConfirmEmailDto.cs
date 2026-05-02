namespace MyApp.DTOs;

public class ConfirmEmailDto
{
    public string Email { get; set; } = string.Empty;
    public string CaptchaId { get; set; } = string.Empty;
    public string CaptchaAnswer { get; set; } = string.Empty;
}
