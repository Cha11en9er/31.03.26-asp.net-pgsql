using System.IdentityModel.Tokens.Jwt;
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
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    /// <summary>Регистрация: JWT не выдаётся до подтверждения email.</summary>
    public async Task<RegisterResult> Register(string email, string password)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(u => u.Email == normalized))
            return RegisterResult.FailUserExists();

        var token = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.AddHours(24);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            EmailConfirmationToken = token,
            EmailConfirmationTokenExpiresAt = expires,
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

        return RegisterResult.Ok(token);
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

    public async Task<bool> ConfirmEmail(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailConfirmationToken == token.Trim());
        if (user == null)
            return false;

        if (user.EmailConfirmationTokenExpiresAt is { } exp && exp < DateTime.UtcNow)
            return false;

        user.IsEmailConfirmed = true;
        user.EmailConfirmationToken = null;
        user.EmailConfirmationTokenExpiresAt = null;

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
    public string? ConfirmationToken { get; init; }

    public static RegisterResult FailUserExists() => new() { UserExists = true };
    public static RegisterResult Ok(string confirmationToken) => new() { ConfirmationToken = confirmationToken };
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
