using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MyApp.Services;

public sealed class CaptchaService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ConcurrentDictionary<string, CaptchaChallenge> _challenges = new();

    public CaptchaService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public CaptchaStartResult CreateChallenge()
    {
        var captchaDir = Path.Combine(_environment.ContentRootPath, "captcha_pic");
        if (!Directory.Exists(captchaDir))
            throw new InvalidOperationException($"Папка captcha не найдена: {captchaDir}");

        var files = Directory.GetFiles(captchaDir)
            .Where(IsSupportedImage)
            .ToArray();

        if (files.Length == 0)
            throw new InvalidOperationException("В папке captcha_pic нет поддерживаемых изображений.");

        var selected = files[RandomNumberGenerator.GetInt32(files.Length)];
        var answer = Path.GetFileNameWithoutExtension(selected).Trim().ToLowerInvariant();
        var id = Guid.NewGuid().ToString("N");

        _challenges[id] = new CaptchaChallenge
        {
            FilePath = selected,
            Answer = answer,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        return new CaptchaStartResult(id, $"/api/auth/captcha/{id}");
    }

    public CaptchaImageResult? GetImage(string captchaId)
    {
        CleanupExpired();
        if (!_challenges.TryGetValue(captchaId, out var item))
            return null;

        if (item.ExpiresAt < DateTime.UtcNow)
        {
            _challenges.TryRemove(captchaId, out _);
            return null;
        }

        return new CaptchaImageResult(item.FilePath, GetContentType(item.FilePath));
    }

    public bool VerifyAndConsume(string captchaId, string answer)
    {
        CleanupExpired();
        if (!_challenges.TryGetValue(captchaId, out var item))
            return false;

        var normalized = (answer ?? string.Empty).Trim().ToLowerInvariant();
        var ok = normalized == item.Answer && item.ExpiresAt >= DateTime.UtcNow;
        _challenges.TryRemove(captchaId, out _);
        return ok;
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _challenges)
        {
            if (pair.Value.ExpiresAt < now)
                _challenges.TryRemove(pair.Key, out _);
        }
    }

    private static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private sealed class CaptchaChallenge
    {
        public string FilePath { get; init; } = string.Empty;
        public string Answer { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
    }
}

public sealed record CaptchaStartResult(string CaptchaId, string CaptchaImageUrl);
public sealed record CaptchaImageResult(string FilePath, string ContentType);
