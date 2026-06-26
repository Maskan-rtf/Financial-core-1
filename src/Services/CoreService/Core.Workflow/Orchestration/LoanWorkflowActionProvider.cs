using Core.Application.Abstractions;
using Core.Domain.Enums;

namespace Core.Workflow.Orchestration;

public sealed class LoanWorkflowActionProvider(ElsaCommandBookmarkReader bookmarkReader) : ILoanWorkflowActionProvider
{
    public async Task<IReadOnlyCollection<LoanWorkflowAction>> GetAllowedActionsAsync(
        Guid caseId,
        string? workflowInstanceId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var commands = await bookmarkReader.GetAllowedCommandsAsync(workflowInstanceId, actorRole, cancellationToken);
        return commands
            .Where(x => x.CaseId == caseId)
            .Select(x => Enum.TryParse<LoanWorkflowAction>(x.CommandName, out var action) ? (LoanWorkflowAction?)action : null)
            .Where(x => x.HasValue && x.Value != LoanWorkflowAction.StageRollback)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
    }
}
