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

        return Ok(new
        {
            user = new
            {
                user.Email,
                user.IsEmailConfirmed,
                account.BalanceRub,
                account.DebtRub,
                account.UpdatedAt
            },
            services = contracts,
            transactions
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

