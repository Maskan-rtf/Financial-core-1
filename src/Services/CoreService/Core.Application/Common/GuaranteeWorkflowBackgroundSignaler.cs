using Core.Application.Abstractions;
using Core.Domain.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Application.Common;

internal static class GuaranteeWorkflowBackgroundSignaler
{
    public static void SignalStatusChanged(
        IServiceScopeFactory serviceScopeFactory,
        ILogger logger,
        Guid caseId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IGuaranteeWorkflowOrchestrator>();
                await orchestrator.SignalGuaranteeCaseAsync(
                    caseId,
                    WorkflowSignals.StatusChanged,
                    payload: null,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Guarantee workflow signal failed for case {CaseId}", caseId);
            }
        });
    }
}
