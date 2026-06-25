using System.Text.Json;
using BuildingBlocks.Application.Common;
using Core.Application.Identity.Common;
using Core.Application.Identity.Common.Options;
using Core.Application.Identity.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace Core.Application.Identity.Services;

public class SmsService : ISmsService
{
    #region Configuration

    private readonly HttpClient _httpClient;
    private readonly ILogger<SmsService> _logger;
    private readonly string _apiBaseUrl;
    private readonly string _apiKey;
    private readonly string _senderNumber;


    private readonly Dictionary<int, string> _smsMessages = new()
    {
        { 1, "ثبت‌نام شما با موفقیت انجام شد. از همراهی شما سپاسگزاریم." },
        { 2, "حساب کاربری شما فعال شد و آماده استفاده است." },
        { 3, "در صورت نیاز به راهنمایی با پشتیبانی تماس بگیرید. اطلاعات ورود شما محرمانه است." },
        { 4, "وضعیت درخواست شما به‌روزرسانی شد." }
    };

    #endregion

    #region Public API

    public SmsService(HttpClient httpClient, IOptions<SmsOptions> options, ILogger<SmsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiBaseUrl = string.IsNullOrWhiteSpace(options.Value.ApiBaseUrl)
            ? "https://api.kavenegar.com"
            : options.Value.ApiBaseUrl.TrimEnd('/');
        _apiKey = string.IsNullOrWhiteSpace(options.Value.ApiKey)
            ? throw new InvalidOperationException(SystemMessages.SmsApiKeyMissing)
            : options.Value.ApiKey;
        _senderNumber = string.IsNullOrWhiteSpace(options.Value.SenderNumber)
            ? throw new InvalidOperationException(SystemMessages.SmsSenderMissing)
            : options.Value.SenderNumber;
    }

    public async Task<bool> SendOtpAsync(string mobileNumber, string otpCode, DateTime validTime)
    {
        try
        {
            var apiUrl = $"{_apiBaseUrl}/v1/{_apiKey}/sms/send.json";

            var messageText = BuildOtpMessage(otpCode, validTime);

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("receptor", mobileNumber),
                new KeyValuePair<string, string>("sender", _senderNumber),
                new KeyValuePair<string, string>("message", messageText),
            });

            var response = await _httpClient.PostAsync(apiUrl, formContent);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Kavenegar SMS error. StatusCode={StatusCode} Body={Body}", (int)response.StatusCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending OTP");
            return false;
        }
    }

    public Task<bool> SendSmsAsync(string mobileNumber, int messageId)
    {
        if (!_smsMessages.TryGetValue(messageId, out var messageText))
            throw new ArgumentException(IdentityMessages.UnknownSmsTemplate);

        return SendRawMessageAsync(mobileNumber, messageText);
    }

    public async Task<bool> SendRawMessageAsync(string mobileNumber, string messageText)
    {
        try
        {
            var url = $"{_apiBaseUrl}/v1/{_apiKey}/sms/send.json";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("receptor", mobileNumber),
                new KeyValuePair<string, string>("sender", _senderNumber),
                new KeyValuePair<string, string>("message", messageText)
            });

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Kavenegar SMS error. StatusCode={StatusCode} Body={Body}", (int)response.StatusCode, error);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {Mobile}", mobileNumber);
            return false;
        }
    }


    public async Task<bool> SendBulkSmsAsync(List<string> mobileNumbers, int messageId)
    {
        try
        {
            if (!_smsMessages.TryGetValue(messageId, out var messageText))
            {
                throw new ArgumentException(IdentityMessages.UnknownSmsTemplate);
            }


            var receptorsJson = JsonSerializer.Serialize(mobileNumbers);
            var sendersJson = JsonSerializer.Serialize(Enumerable.Repeat(_senderNumber, mobileNumbers.Count));
            var messagesJson = JsonSerializer.Serialize(Enumerable.Repeat(messageText, mobileNumbers.Count));

            string url = $"{_apiBaseUrl}/v1/{_apiKey}/sms/sendarray.json";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("receptor", receptorsJson),
                new KeyValuePair<string, string>("sender", sendersJson),
                new KeyValuePair<string, string>("message", messagesJson)
            });

            var response = await _httpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Bulk SMS");
            return false;
        }
    }

    #endregion

    #region Private

    private string BuildOtpMessage(string otpCode, DateTime validUntil)
    {
        var validMinutes = (int)(validUntil - DateTime.UtcNow).TotalMinutes;

        return
            $"کد ورود شما: {otpCode}\n\n" +
            $"این کد تا {validMinutes} دقیقه معتبر است.\n" +
            $"کد را با کسی به اشتراک نگذارید.\n\n" +
            $"صندوق پژوهش و فناوری غیردولتی مسکن";
    }

    #endregion
}