using Core.Application.Identity.DTOs.User;

namespace Core.Application.Identity.Interfaces;

public interface ICaptchaService
{
    CaptchaChallengeDto Create();
    bool TryConsume(string? captchaId, string? code);
    string IssueResendToken(string phoneNumber);
    bool TryConsumeResendToken(string? token, string phoneNumber);
    bool IsPasswordLocked(string phoneNumber);
    void RegisterPasswordFailure(string phoneNumber);
    void ClearPasswordFailures(string phoneNumber);
}
