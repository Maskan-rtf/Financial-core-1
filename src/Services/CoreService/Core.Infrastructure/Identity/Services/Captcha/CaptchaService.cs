using System.Security.Cryptography;
using System.Text;
using Core.Application.Identity.DTOs.User;
using Core.Application.Identity.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Core.Infrastructure.Identity.Services.Captcha;

public sealed class CaptchaService(IMemoryCache cache) : ICaptchaService
{
    private static readonly char[] Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
    private const int CodeLength = 5;
    private static readonly TimeSpan CaptchaTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ResendTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(10);
    private const int MaxPasswordFailures = 5;

    public CaptchaChallengeDto Create()
    {
        var code = NewCode();
        var id = Guid.NewGuid().ToString("N");
        cache.Set(CaptchaKey(id), code, CaptchaTtl);
        return new CaptchaChallengeDto
        {
            CaptchaId = id,
            Image = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(RenderSvg(code)))
        };
    }

    public bool TryConsume(string? captchaId, string? code)
    {
        if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(code))
            return false;

        var key = CaptchaKey(captchaId.Trim());
        if (!cache.TryGetValue(key, out string? expected) || string.IsNullOrEmpty(expected))
            return false;

        cache.Remove(key);
        return string.Equals(expected, Normalize(code), StringComparison.OrdinalIgnoreCase);
    }

    public string IssueResendToken(string phoneNumber)
    {
        var token = Guid.NewGuid().ToString("N");
        cache.Set(ResendKey(token), phoneNumber, ResendTtl);
        return token;
    }

    public bool TryConsumeResendToken(string? token, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var key = ResendKey(token.Trim());
        if (!cache.TryGetValue(key, out string? phone) || !string.Equals(phone, phoneNumber, StringComparison.Ordinal))
            return false;

        cache.Remove(key);
        return true;
    }

    public bool IsPasswordLocked(string phoneNumber)
        => cache.TryGetValue(FailKey(phoneNumber), out int count) && count >= MaxPasswordFailures;

    public void RegisterPasswordFailure(string phoneNumber)
    {
        var key = FailKey(phoneNumber);
        var count = cache.TryGetValue(key, out int current) ? current + 1 : 1;
        cache.Set(key, count, LockTtl);
    }

    public void ClearPasswordFailures(string phoneNumber)
        => cache.Remove(FailKey(phoneNumber));

    private static string CaptchaKey(string id) => "captcha:" + id;
    private static string ResendKey(string token) => "otp-resend:" + token;
    private static string FailKey(string phone) => "password-fail:" + phone;

    private static string NewCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(ch switch
            {
                '۰' => '0',
                '۱' => '1',
                '۲' => '2',
                '۳' => '3',
                '۴' => '4',
                '۵' => '5',
                '۶' => '6',
                '۷' => '7',
                '۸' => '8',
                '۹' => '9',
                '٠' => '0',
                '١' => '1',
                '٢' => '2',
                '٣' => '3',
                '٤' => '4',
                '٥' => '5',
                '٦' => '6',
                '٧' => '7',
                '٨' => '8',
                '٩' => '9',
                _ => char.ToUpperInvariant(ch)
            });
        }
        return builder.ToString();
    }

    private static string RenderSvg(string code)
    {
        var rng = RandomNumberGenerator.Create();
        int Next(int max) => RandomNumberGenerator.GetInt32(max);
        var noise = new StringBuilder();
        for (var i = 0; i < 6; i++)
        {
            noise.Append("<line x1=\"").Append(Next(160))
                .Append("\" y1=\"").Append(Next(56))
                .Append("\" x2=\"").Append(Next(160))
                .Append("\" y2=\"").Append(Next(56))
                .Append("\" stroke=\"#8fc0d2\" stroke-width=\"1\" />");
        }

        var letters = new StringBuilder();
        for (var i = 0; i < code.Length; i++)
        {
            var x = 16 + (i * 26);
            var y = 34 + Next(8);
            var rotate = Next(21) - 10;
            letters.Append("<text x=\"").Append(x)
                .Append("\" y=\"").Append(y)
                .Append("\" fill=\"#035e84\" font-size=\"24\" font-family=\"Tahoma, sans-serif\" font-weight=\"700\" transform=\"rotate(")
                .Append(rotate).Append(' ').Append(x).Append(' ').Append(y)
                .Append(")\">")
                .Append(code[i])
                .Append("</text>");
        }

        rng.Dispose();
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"148\" height=\"52\" viewBox=\"0 0 148 52\">"
            + "<rect width=\"148\" height=\"52\" rx=\"8\" fill=\"#f2f8fb\"/>"
            + noise
            + letters
            + "</svg>";
    }
}
