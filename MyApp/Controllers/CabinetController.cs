using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.DTOs;
using MyApp.Models;
using MyApp.Services;

namespace MyApp.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class CabinetController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<CabinetController> _logger;

    public CabinetController(AppDbContext context, IEmailSender emailSender, ILogger<CabinetController> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();

        var account = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId)
            ?? new UserAccount { UserId = userId.Value, BalanceRub = 0, DebtRub = 0, UpdatedAt = DateTime.UtcNow };

        var contracts = await _context.Contracts
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.ContractNumber,
                c.AmountRub,
                c.CreatedAt,
                c.EffectiveFrom,
                ProductName = c.Product.Name
            })
            .ToListAsync();

        var transactions = await _context.BalanceTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new
            {
                t.Id,
                t.Type,
                t.Description,
                t.AmountRub,
                t.CreatedAt
            })
            .ToListAsync();

        var legal = await _context.LegalEntityProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        return Ok(new
        {
            user = new
            {
                user.Email,
                user.IsEmailConfirmed,
                user.AccountType,
                accountTypeLabel = user.AccountType == "Legal" ? "Юридическое лицо" : "Физическое лицо",
                account.BalanceRub,
                account.DebtRub,
                account.UpdatedAt
            },
            legalEntity = legal == null ? null : new
            {
                legal.CompanyFullName,
                legal.CompanyShortName,
                legal.Inn,
                legal.Ogrn,
                legal.Kpp,
                legal.DirectorFullName,
                legal.DirectorBirthDate,
                legal.DocumentFileName,
                legal.VerifiedAt
            },
            services = contracts,
            transactions
        });
    }

    [HttpPost("legal-verification")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> SubmitLegalVerification([FromForm] LegalVerificationFormDto dto, IFormFile? document)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (document == null || document.Length == 0)
            return BadRequest(new { message = "Загрузите PDF-документ, подтверждающий статус юридического лица." });

        var ext = Path.GetExtension(document.FileName).ToLowerInvariant();
        if (ext != ".pdf")
            return BadRequest(new { message = "Допустим только формат PDF." });

        if (document.Length > 5 * 1024 * 1024)
            return BadRequest(new { message = "Размер файла не должен превышать 5 МБ." });

        var inn = (dto.Inn ?? string.Empty).Trim();
        var ogrn = (dto.Ogrn ?? string.Empty).Trim();
        var kpp = (dto.Kpp ?? string.Empty).Trim();

        if (inn.Length != 10 || !inn.All(char.IsDigit))
            return BadRequest(new { message = "ИНН должен содержать ровно 10 цифр." });
        if (ogrn.Length != 13 || !ogrn.All(char.IsDigit))
            return BadRequest(new { message = "ОГРН должен содержать ровно 13 цифр." });
        if (kpp.Length != 9 || !kpp.All(char.IsDigit))
            return BadRequest(new { message = "КПП должен содержать ровно 9 цифр." });

        if (string.IsNullOrWhiteSpace(dto.CompanyFullName) ||
            string.IsNullOrWhiteSpace(dto.CompanyShortName) ||
            string.IsNullOrWhiteSpace(dto.DirectorFullName) ||
            dto.DirectorBirthDate == default)
        {
            return BadRequest(new { message = "Заполните все поля реквизитов организации." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();

        await using var ms = new MemoryStream();
        await document.CopyToAsync(ms);

        var now = DateTime.UtcNow;
        var profile = await _context.LegalEntityProfiles.FirstOrDefaultAsync(p => p.UserId == userId.Value);
        if (profile == null)
        {
            profile = new LegalEntityProfile { UserId = userId.Value };
            _context.LegalEntityProfiles.Add(profile);
        }

        profile.CompanyFullName = dto.CompanyFullName.Trim();
        profile.CompanyShortName = dto.CompanyShortName.Trim();
        profile.Inn = inn;
        profile.Ogrn = ogrn;
        profile.Kpp = kpp;
        profile.DirectorFullName = dto.DirectorFullName.Trim();
        profile.DirectorBirthDate = dto.DirectorBirthDate;
        profile.DocumentFileName = Path.GetFileName(document.FileName);
        profile.DocumentContent = ms.ToArray();
        profile.VerifiedAt = now;

        user.AccountType = "Legal";

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Верификация завершена. Кабинет переведён в режим юридического лица.",
            accountType = "Legal"
        });
    }

    [HttpPost("services")]
    public async Task<IActionResult> ConnectService([FromBody] CreateContractDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product == null)
            return NotFound(new { message = "Услуга не найдена." });

        var now = DateTime.UtcNow;
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            ProductId = product.Id,
            ContractNumber = $"TGK2-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            AmountRub = product.PriceRub,
            EffectiveFrom = now.Date,
            CreatedAt = now
        };

        _context.Contracts.Add(contract);

        var account = await _context.UserAccounts.FirstOrDefaultAsync(a => a.UserId == userId.Value);
        if (account == null)
        {
            account = new UserAccount
            {
                UserId = userId.Value,
                BalanceRub = 0,
                DebtRub = 0
            };
            _context.UserAccounts.Add(account);
        }

        account.DebtRub += product.PriceRub;
        account.UpdatedAt = now;

        _context.BalanceTransactions.Add(new BalanceTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Type = "Начисление",
            Description = $"Подключение услуги: {product.Name}",
            AmountRub = product.PriceRub,
            CreatedAt = now
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Услуга подключена и договор активирован.",
            contract = new
            {
                contract.Id,
                contract.ContractNumber,
                contract.AmountRub,
                contract.CreatedAt,
                ProductName = product.Name
            }
        });
    }

    [HttpPost("top-up")]
    public async Task<IActionResult> TopUp([FromBody] TopUpBalanceDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        if (dto.AmountRub <= 0)
            return BadRequest(new { message = "Сумма пополнения должна быть больше нуля." });

        var now = DateTime.UtcNow;
        var account = await _context.UserAccounts.FirstOrDefaultAsync(a => a.UserId == userId.Value);
        if (account == null)
        {
            account = new UserAccount { UserId = userId.Value, BalanceRub = 0, DebtRub = 0, UpdatedAt = now };
            _context.UserAccounts.Add(account);
        }

        var amount = dto.AmountRub;
        if (account.DebtRub > 0)
        {
            var debtReduction = Math.Min(account.DebtRub, amount);
            account.DebtRub -= debtReduction;
            amount -= debtReduction;
        }

        if (amount > 0)
            account.BalanceRub += amount;

        account.UpdatedAt = now;

        _context.BalanceTransactions.Add(new BalanceTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Type = "Пополнение",
            Description = "Пополнение баланса через личный кабинет",
            AmountRub = dto.AmountRub,
            CreatedAt = now
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Баланс успешно пополнен.",
            account.BalanceRub,
            account.DebtRub
        });
    }

    [HttpPost("send-receipt")]
    public async Task<IActionResult> SendReceipt()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();

        var account = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId)
            ?? new UserAccount { UserId = userId.Value, BalanceRub = 0, DebtRub = 0 };

        var contracts = await _context.Contracts
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ReceiptServiceLine(
                c.ContractNumber,
                c.Product.Name,
                c.AmountRub))
            .ToListAsync();

        var paidRub = await _context.BalanceTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Type == "Пополнение")
            .SumAsync(t => t.AmountRub);

        var receipt = new ReceiptEmailData
        {
            Services = contracts,
            TotalServicesRub = contracts.Sum(c => c.AmountRub),
            PaidRub = paidRub,
            DebtRub = account.DebtRub,
            BalanceRub = account.BalanceRub
        };

        try
        {
            await _emailSender.SendReceipt(user.Email, receipt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receipt email send failed for {Email}", user.Email);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Не удалось отправить квитанцию. " + ex.Message });
        }

        return Ok(new { message = $"Квитанция отправлена на {user.Email}." });
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}

