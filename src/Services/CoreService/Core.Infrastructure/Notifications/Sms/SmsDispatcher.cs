using Core.Application.Identity.Common.Options;
using Core.Application.Identity.Interfaces;
using Core.Application.Logging;
using Core.Application.Notifications.Sms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Infrastructure.Notifications.Sms;

public sealed class SmsDispatcher(
    ISmsService smsService,
    ISmsAuditStore auditStore,
    SmsDispatchQueue queue,
    IOptions<SmsOptions> options,
    ILogger<SmsDispatcher> logger) : ISmsDispatcher
{
    public async Task<bool> SendImmediateAsync(
        SmsTemplateId templateId,
        string mobile,
        IReadOnlyDictionary<string, string>? args,
        CancellationToken cancellationToken = default)
    {
        var message = SmsTemplateCatalog.Render(templateId, args);
        var success = await smsService.SendRawMessageAsync(mobile, message);

        await auditStore.AppendAsync(new SmsAuditEntry
        {
            TemplateId = templateId,
            Mobile = mobile,
            Message = message,
            Success = success,
            Error = success ? null : "Provider returned failure"
        }, cancellationToken);

        if (success)
        {
            ApplicationLog.Completed(logger,
                "SMS sent immediately — template {TemplateId} to {Mobile}",
                templateId, mobile);
        }
        else
        {
            logger.LogWarning("SMS send failed for template {TemplateId} to {Mobile}", templateId, mobile);
        }

        return success;
    }

    public Task EnqueueAsync(
        SmsTemplateId templateId,
        string mobile,
        IReadOnlyDictionary<string, string>? args,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.QueueEnabled)
            return SendImmediateAsync(templateId, mobile, args, cancellationToken);

        var notBefore = DateTimeOffset.UtcNow.Add(delay ?? TimeSpan.Zero);
        ApplicationLog.Completed(logger,
            "SMS enqueued — template {TemplateId} to {Mobile}, not before {NotBeforeUtc}",
            templateId, mobile, notBefore);

        return queue.EnqueueAsync(new SmsQueuedMessage
        {
            TemplateId = templateId,
            Mobile = mobile,
            Args = args,
            NotBeforeUtc = notBefore
        }, cancellationToken).AsTask();
    }

    public Task EnqueueRawAsync(
        string mobile,
        string message,
        Guid? caseId = null,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.QueueEnabled)
            return SendImmediateRawAsync(mobile, message, caseId, cancellationToken);

        var notBefore = DateTimeOffset.UtcNow.Add(delay ?? TimeSpan.Zero);
        ApplicationLog.Completed(logger,
            "SMS enqueued (raw) to {Mobile}, not before {NotBeforeUtc}",
            mobile, notBefore);

        return queue.EnqueueAsync(new SmsQueuedMessage
        {
            RawMessage = message,
            Mobile = mobile,
            NotBeforeUtc = notBefore,
            CaseId = caseId
        }, cancellationToken).AsTask();
    }

    public async Task<bool> SendImmediateRawAsync(
        string mobile,
        string message,
        Guid? caseId = null,
        CancellationToken cancellationToken = default)
    {
        var success = await smsService.SendRawMessageAsync(mobile, message);

        await auditStore.AppendAsync(new SmsAuditEntry
        {
            TemplateId = SmsTemplateId.CaseStatusChanged,
            Mobile = mobile,
            Message = message,
            Success = success,
            Error = success ? null : "Provider returned failure",
            CaseId = caseId
        }, cancellationToken);

        if (success)
        {
            ApplicationLog.Completed(logger, "SMS sent immediately (raw) to {Mobile}", mobile);
        }
        else
        {
            logger.LogWarning("Raw SMS send failed to {Mobile}", mobile);
        }

        return success;
    }
}
