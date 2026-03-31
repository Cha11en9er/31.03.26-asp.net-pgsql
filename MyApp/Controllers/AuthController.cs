using Microsoft.AspNetCore.Mvc;
using MyApp.DTOs;
using MyApp.Services;

namespace MyApp.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "Email и пароль обязательны." });

        var result = await _authService.Register(dto.Email, dto.Password);

        if (result.UserExists)
            return Conflict(new { message = "Пользователь с таким email уже существует." });

        return Ok(new
        {
            message = "Аккаунт создан. Подтвердите email токеном (учебный проект: токен ниже).",
            confirmationToken = result.ConfirmationToken
        });
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Token))
            return BadRequest(new { message = "Токен обязателен." });

        var ok = await _authService.ConfirmEmail(dto.Token);
        if (!ok)
            return BadRequest(new { message = "Неверный или просроченный токен." });

        return Ok(new { message = "Email подтверждён. Можно входить." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "Email и пароль обязательны." });

        var result = await _authService.Login(dto.Email, dto.Password);

        if (result.Failure == LoginFailureKind.EmailNotConfirmed)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Сначала подтвердите email (confirm-email)." });

        if (result.Failure == LoginFailureKind.InvalidCredentials)
            return Unauthorized();

        return Ok(new { token = result.Token });
    }
}
