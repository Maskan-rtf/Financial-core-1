using Core.Application.Abstractions;
using Core.Domain.Enums;

namespace Core.Workflow.Orchestration;

public sealed class GuaranteeWorkflowActionProvider(ElsaCommandBookmarkReader bookmarkReader) : IGuaranteeWorkflowActionProvider
{
    public async Task<IReadOnlyCollection<GuaranteeWorkflowAction>> GetAllowedActionsAsync(
        Guid caseId,
        string? workflowInstanceId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var commands = await bookmarkReader.GetAllowedCommandsAsync(workflowInstanceId, actorRole, cancellationToken);
        return commands
            .Where(x => x.CaseId == caseId)
            .Select(x => Enum.TryParse<GuaranteeWorkflowAction>(x.CommandName, out var action) ? (GuaranteeWorkflowAction?)action : null)
            .Where(x => x.HasValue && x.Value != GuaranteeWorkflowAction.StageRollback)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
    }
}
