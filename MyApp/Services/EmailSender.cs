using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace MyApp.Services;

public sealed record ReceiptServiceLine(string ContractNumber, string ProductName, decimal AmountRub);

public sealed class ReceiptEmailData
{
    public required IReadOnlyList<ReceiptServiceLine> Services { get; init; }
    public decimal TotalServicesRub { get; init; }
    public decimal PaidRub { get; init; }
    public decimal DebtRub { get; init; }
    public decimal BalanceRub { get; init; }
}

public interface IEmailSender
{
    Task SendRegistrationCode(string toEmail, string code);
    Task SendReceipt(string toEmail, ReceiptEmailData data);
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

    public Task SendRegistrationCode(string toEmail, string code) =>
        SendEmailAsync(
            toEmail,
            "Код подтверждения регистрации",
            $"Ваш код подтверждения: {code}\n\nКод действует 15 минут.");

    public Task SendReceipt(string toEmail, ReceiptEmailData data)
    {
        var body = BuildReceiptBody(data);
        return SendEmailAsync(toEmail, "Квитанция по услугам ТГК-2", body);
    }

    private static string BuildReceiptBody(ReceiptEmailData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Квитанция по подключённым услугам");
        sb.AppendLine();
        sb.AppendLine($"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine();

        if (data.Services.Count == 0)
        {
            sb.AppendLine("Подключённых услуг нет.");
        }
        else
        {
            sb.AppendLine("Ваши услуги:");
            foreach (var service in data.Services)
            {
                sb.AppendLine($"- {service.ProductName} (договор {service.ContractNumber}) — {FormatMoney(service.AmountRub)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Итого по услугам: {FormatMoney(data.TotalServicesRub)}");
        sb.AppendLine($"Оплачено: {FormatMoney(data.PaidRub)}");
        sb.AppendLine($"Осталось к оплате: {FormatMoney(data.DebtRub)}");
        sb.AppendLine($"Баланс на счёте: {FormatMoney(data.BalanceRub)}");
        sb.AppendLine();
        sb.AppendLine("—");
        sb.AppendLine("ТГК-2 • Портал клиентов");

        return sb.ToString();
    }

    private static string FormatMoney(decimal amount) =>
        amount.ToString("N2", CultureInfo.GetCultureInfo("ru-RU")) + " ₽";

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var host = _configuration["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host не настроен.");
        var portRaw = _configuration["Smtp:Port"] ?? "587";
        var user = RequireValidEmail(_configuration["Smtp:Username"], "Smtp:Username");
        var password = _configuration["Smtp:Password"] ?? throw new InvalidOperationException("Smtp:Password не настроен.");
        var from = RequireValidEmail(_configuration["Smtp:FromEmail"] ?? user, "Smtp:FromEmail");
        var to = RequireValidEmail(toEmail, "получатель");
        var fromName = (_configuration["Smtp:FromName"] ?? "MyApp").Trim();
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
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(to);

        _logger.LogInformation("Sending email \"{Subject}\" to {Email}", subject, toEmail);
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

    private static string RequireValidEmail(string? value, string fieldName)
    {
        var email = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(email))
            throw new InvalidOperationException($"{fieldName} не настроен. Укажите реальный email в .env.");

        if (!MailAddress.TryCreate(email, out _))
            throw new InvalidOperationException(
                $"{fieldName} имеет неверный формат: «{email}». " +
                "В MyApp/.env замените заглушку на реальный Gmail, например: Smtp__FromEmail=you@gmail.com");

        return email;
    }
}
