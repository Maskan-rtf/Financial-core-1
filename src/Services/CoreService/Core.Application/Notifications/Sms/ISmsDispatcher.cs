namespace Core.Application.Notifications.Sms;

public interface ISmsDispatcher
{
    Task<bool> SendImmediateAsync(
        SmsTemplateId templateId,
        string mobile,
        IReadOnlyDictionary<string, string>? args,
        CancellationToken cancellationToken = default);

    Task EnqueueAsync(
        SmsTemplateId templateId,
        string mobile,
        IReadOnlyDictionary<string, string>? args,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default);

    Task EnqueueRawAsync(
        string mobile,
        string message,
        Guid? caseId = null,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default);

    Task<bool> SendImmediateRawAsync(
        string mobile,
        string message,
        Guid? caseId = null,
        CancellationToken cancellationToken = default);
}
