using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyApp.Data;
using MyApp.Models;

namespace MyApp.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IEmailSender _emailSender;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(AppDbContext context, IConfiguration config, IEmailSender emailSender)
    {
        _context = context;
        _config = config;
        _emailSender = emailSender;
    }

    /// <summary>Регистрация: отправка кода на email и ожидание подтверждения.</summary>
    public async Task<RegisterResult> Register(string email, string password)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var expires = DateTime.UtcNow.AddMinutes(15);
        var existing = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        if (existing is { IsEmailConfirmed: true })
            return RegisterResult.FailUserExists();

        if (existing is not null)
        {
            existing.PasswordHash = _passwordHasher.HashPassword(existing, password);
            existing.EmailConfirmationCode = code;
            existing.EmailConfirmationCodeExpiresAt = expires;
            existing.EmailCodeVerifiedAt = null;
            await _context.SaveChangesAsync();
        }
        else
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalized,
                EmailConfirmationCode = code,
                EmailConfirmationCodeExpiresAt = expires,
                EmailCodeVerifiedAt = null,
                IsEmailConfirmed = false
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            _context.Users.Add(user);
            _context.UserAccounts.Add(new UserAccount
            {
                UserId = user.Id,
                BalanceRub = 0,
                DebtRub = 0,
                UpdatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        await _emailSender.SendRegistrationCode(normalized, code);
        return RegisterResult.Ok();
    }

    public async Task<LoginResult> Login(string email, string password)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        if (user == null)
            return LoginResult.InvalidCredentials();

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
            return LoginResult.InvalidCredentials();

        if (!user.IsEmailConfirmed)
            return LoginResult.EmailNotConfirmed();

        return LoginResult.Ok(GenerateJwt(user));
    }

    public async Task<bool> VerifyEmailCode(string email, string code)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = email.Trim().ToLowerInvariant();
        var expectedCode = code.Trim();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        if (user == null)
            return false;

        if (user.IsEmailConfirmed)
            return false;

        if (user.EmailConfirmationCodeExpiresAt is { } exp && exp < DateTime.UtcNow)
            return false;

        if (!string.Equals(user.EmailConfirmationCode, expectedCode, StringComparison.Ordinal))
            return false;

        user.EmailCodeVerifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ConfirmEmailAfterCaptcha(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var normalized = email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        if (user == null)
            return false;

        if (user.IsEmailConfirmed)
            return true;

        if (user.EmailCodeVerifiedAt is not { } verifiedAt)
            return false;

        if (verifiedAt < DateTime.UtcNow.AddMinutes(-10))
            return false;

        user.IsEmailConfirmed = true;
        user.EmailConfirmationCode = null;
        user.EmailConfirmationCodeExpiresAt = null;
        user.EmailCodeVerifiedAt = null;

        await _context.SaveChangesAsync();
        return true;
    }

    private string GenerateJwt(User user)
    {
        var keyString = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured");
        var issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured");
        var audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(keyString);
        }
        catch
        {
            keyBytes = Encoding.UTF8.GetBytes(keyString);
        }

        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:Key is too short. Use at least 32+ bytes.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class RegisterResult
{
    public bool UserExists { get; init; }

    public static RegisterResult FailUserExists() => new() { UserExists = true };
    public static RegisterResult Ok() => new();
}

public sealed class LoginResult
{
    public string? Token { get; init; }
    public LoginFailureKind Failure { get; init; }

    public static LoginResult Ok(string token) => new() { Token = token, Failure = LoginFailureKind.None };
    public static LoginResult InvalidCredentials() => new() { Failure = LoginFailureKind.InvalidCredentials };
    public static LoginResult EmailNotConfirmed() => new() { Failure = LoginFailureKind.EmailNotConfirmed };
}

public enum LoginFailureKind
{
    None,
    InvalidCredentials,
    EmailNotConfirmed
}
