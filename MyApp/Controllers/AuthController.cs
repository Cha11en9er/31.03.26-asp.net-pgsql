using Microsoft.AspNetCore.Mvc;
using MyApp.DTOs;
using MyApp.Services;

namespace MyApp.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly CaptchaService _captchaService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, CaptchaService captchaService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _captchaService = captchaService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "Email и пароль обязательны." });

        RegisterResult result;
        try
        {
            result = await _authService.Register(dto.Email, dto.Password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration email send failed for {Email}", dto.Email);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Не удалось отправить код на email. " + ex.Message });
        }

        if (result.UserExists)
            return Conflict(new { message = "Пользователь с таким email уже существует." });

        return Ok(new { message = "Аккаунт создан. Код подтверждения отправлен на email." });
    }

    [HttpPost("verify-email-code")]
    public async Task<IActionResult> VerifyEmailCode([FromBody] VerifyEmailCodeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { message = "Email и код обязательны." });

        var ok = await _authService.VerifyEmailCode(dto.Email, dto.Code);
        if (!ok)
            return BadRequest(new { message = "Неверный или просроченный код." });

        var challenge = _captchaService.CreateChallenge();
        return Ok(new
        {
            message = "Код подтверждён. Теперь решите капчу.",
            captchaId = challenge.CaptchaId,
            captchaImageUrl = challenge.CaptchaImageUrl
        });
    }

    [HttpGet("captcha/{captchaId}")]
    public IActionResult GetCaptchaImage([FromRoute] string captchaId)
    {
        var image = _captchaService.GetImage(captchaId);
        if (image == null)
            return NotFound(new { message = "Капча не найдена или истекла." });

        return PhysicalFile(image.FilePath, image.ContentType);
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.CaptchaId) ||
            string.IsNullOrWhiteSpace(dto.CaptchaAnswer))
        {
            return BadRequest(new { message = "Email и поля капчи обязательны." });
        }

        var captchaOk = _captchaService.VerifyAndConsume(dto.CaptchaId, dto.CaptchaAnswer);
        if (!captchaOk)
            return BadRequest(new { message = "Капча введена неверно или истекла." });

        var ok = await _authService.ConfirmEmailAfterCaptcha(dto.Email);
        if (!ok)
            return BadRequest(new { message = "Сначала подтвердите код из email." });

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
