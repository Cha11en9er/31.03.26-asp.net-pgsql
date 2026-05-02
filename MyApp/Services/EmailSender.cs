using System.Net;
using System.Net.Mail;

namespace MyApp.Services;

public interface IEmailSender
{
    Task SendRegistrationCode(string toEmail, string code);
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendRegistrationCode(string toEmail, string code)
    {
        var host = _configuration["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host не настроен.");
        var portRaw = _configuration["Smtp:Port"] ?? "587";
        var user = _configuration["Smtp:Username"] ?? throw new InvalidOperationException("Smtp:Username не настроен.");
        var password = _configuration["Smtp:Password"] ?? throw new InvalidOperationException("Smtp:Password не настроен.");
        var from = _configuration["Smtp:FromEmail"] ?? user;
        var fromName = _configuration["Smtp:FromName"] ?? "MyApp";
        var enableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var parsed) ? parsed : true;

        if (!int.TryParse(portRaw, out var port))
            throw new InvalidOperationException("Smtp:Port должен быть числом.");

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = new NetworkCredential(user, password),
            Timeout = 20000
        };

        using var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = "Код подтверждения регистрации",
            Body = $"Ваш код подтверждения: {code}\n\nКод действует 15 минут.",
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        _logger.LogInformation("Sending registration code email to {Email}", toEmail);
        try
        {
            await client.SendMailAsync(message);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP send failed for {Email}. Host={Host}, Port={Port}, Ssl={Ssl}", toEmail, host, port, enableSsl);
            throw new InvalidOperationException("SMTP error: " + ex.Message, ex);
        }
    }
}
