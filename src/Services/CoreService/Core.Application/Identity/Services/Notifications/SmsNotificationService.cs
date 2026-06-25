using Core.Application.Identity.Common.Options;
using Core.Application.Identity.Interfaces;
using Core.Application.Identity.Notifications;
using Microsoft.Extensions.Options;
using Serilog;


namespace Core.Application.Identity.Services.Notifications;

public class SmsNotificationService : ISmsNotificationService
{
    private readonly ISmsService _smsService;

    public SmsNotificationService(ISmsService smsService)
    {
        _smsService = smsService;
    }

    #region Public API

    public async Task SendOtpNotificationAsync(string mobileNumber, string otpCode, DateTime validTime, CancellationToken cancellationToken = default)
    {
        await _smsService.SendOtpAsync(mobileNumber, otpCode, validTime);
    }
    public async Task SendSmsNotificationAsync(string mobileNumber, int messageId, CancellationToken cancellationToken = default)
    {
        await _smsService.SendSmsAsync(mobileNumber, messageId);
    }

    public async Task SendBulkSmsNotificationAsync(List<string> mobileNumbers, int messageId, CancellationToken cancellationToken = default)
    {
        await _smsService.SendBulkSmsAsync(mobileNumbers, messageId);
    }

    #endregion
}