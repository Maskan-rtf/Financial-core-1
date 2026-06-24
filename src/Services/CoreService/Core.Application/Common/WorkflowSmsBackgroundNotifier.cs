using Core.Application.Notifications.Sms;
using Core.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Application.Common;

internal static class WorkflowSmsBackgroundNotifier
{
    public static void NotifyGuaranteeStepChange(
        IServiceScopeFactory serviceScopeFactory,
        ILogger logger,
        Guid caseId,
        string applicantUserId,
        string caseNumber,
        int fromStatus,
        int toStatus)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var notifier = scope.ServiceProvider.GetRequiredService<IWorkflowSmsNotifier>();
                await notifier.NotifyStepChangeAsync(
                    new WorkflowSmsNotification(
                        CaseModuleType.Guarantee,
                        caseId,
                        applicantUserId,
                        caseNumber,
                        fromStatus,
                        toStatus),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Workflow SMS notification failed for guarantee case {CaseId}", caseId);
            }
        });
    }
}
