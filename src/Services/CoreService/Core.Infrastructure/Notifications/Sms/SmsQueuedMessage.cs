using Core.Application.Notifications.Sms;

namespace Core.Infrastructure.Notifications.Sms;

public sealed class SmsQueuedMessage
{
    public SmsTemplateId? TemplateId { get; init; }

    public string? RawMessage { get; init; }

    public required string Mobile { get; init; }

    public IReadOnlyDictionary<string, string>? Args { get; init; }

    public DateTimeOffset NotBeforeUtc { get; init; }

    public Guid? CaseId { get; init; }
}
