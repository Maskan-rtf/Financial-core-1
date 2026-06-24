using Core.Application.Abstractions;
using Core.Application.Logging;
using Core.Application.Notifications.Sms;
using Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Infrastructure.Notifications.Sms;

public sealed class WorkflowSmsNotifier(
    ICoreDbContext dbContext,
    ISmsDispatcher smsDispatcher,
    IWorkflowSmsCatalog catalog,
    IOptions<WorkflowSmsOptions> options,
    ILogger<WorkflowSmsNotifier> logger) : IWorkflowSmsNotifier, ICaseWorkflowSmsNotifier
{
    public Task NotifyStepChangeAsync(
        WorkflowSmsNotification notification,
        CancellationToken cancellationToken = default)
        => NotifyInternalAsync(
            notification.Module,
            notification.CaseId,
            notification.ApplicantUserId,
            notification.CaseNumber,
            notification.FromStatus,
            notification.ToStatus,
            cancellationToken);

    public Task NotifyStatusChangeAsync(
        Guid caseId,
        string applicantUserId,
        string caseNumber,
        CaseStatus from,
        CaseStatus to,
        WorkflowAction action,
        CancellationToken cancellationToken = default)
        => NotifyInternalAsync(
            CaseModuleType.Investment,
            caseId,
            applicantUserId,
            caseNumber,
            (int)from,
            (int)to,
            cancellationToken);

    private async Task NotifyInternalAsync(
        CaseModuleType module,
        Guid caseId,
        string applicantUserId,
        string caseNumber,
        int fromStatus,
        int toStatus,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !catalog.IsGloballyEnabled)
            return;

        if (fromStatus == toStatus)
            return;

        if (string.IsNullOrWhiteSpace(caseNumber))
        {
            logger.LogDebug("Skipping workflow SMS for case {CaseId}: case number missing", caseId);
            return;
        }

        var step = catalog.FindStep(module, toStatus);
        if (step is null)
        {
            logger.LogDebug(
                "Skipping workflow SMS for case {CaseId}: no config for module {Module} status {Status}",
                caseId, module, toStatus);
            return;
        }

        if (!step.Enabled)
        {
            logger.LogDebug(
                "Skipping workflow SMS for case {CaseId}: step {Module}/{Status} is disabled in config",
                caseId, module, toStatus);
            return;
        }

        var mobile = await ResolveApplicantMobileAsync(applicantUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(mobile))
        {
            logger.LogDebug("Skipping workflow SMS for case {CaseId}: applicant mobile not found", caseId);
            return;
        }

        var statusTitle = WorkflowSmsMessageBuilder.ResolveStatusTitle(module, toStatus);
        var message = WorkflowSmsMessageBuilder.Build(step, catalog.Signature, caseNumber, statusTitle);

        await smsDispatcher.EnqueueRawAsync(mobile, message, caseId, cancellationToken: cancellationToken);

        ApplicationLog.Completed(logger,
            "Workflow SMS queued for case {CaseId} ({CaseNumber}) — module {Module}, {FromStatus} → {ToStatus}",
            caseId, caseNumber, module, fromStatus, toStatus);
    }

    private async Task<string?> ResolveApplicantMobileAsync(string applicantUserId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(applicantUserId, out var userId))
            return null;

        return await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.PhoneNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
