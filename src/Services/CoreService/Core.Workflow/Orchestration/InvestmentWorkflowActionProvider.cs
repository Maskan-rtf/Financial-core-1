using Core.Application.Abstractions;
using Core.Domain.Enums;

namespace Core.Workflow.Orchestration;

public sealed class InvestmentWorkflowActionProvider(ElsaCommandBookmarkReader bookmarkReader) : IInvestmentWorkflowActionProvider
{
    public async Task<IReadOnlyCollection<WorkflowAction>> GetAllowedActionsAsync(
        Guid caseId,
        string? workflowInstanceId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var commands = await bookmarkReader.GetAllowedCommandsAsync(workflowInstanceId, actorRole, cancellationToken);
        return commands
            .Where(x => x.CaseId == caseId)
            .Select(x => Enum.TryParse<WorkflowAction>(x.CommandName, out var action) ? (WorkflowAction?)action : null)
            .Where(x => x.HasValue && x.Value != WorkflowAction.StageRollback)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
    }
}
