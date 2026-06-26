using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface ILoanWorkflowActionProvider
{
    Task<IReadOnlyCollection<LoanWorkflowAction>> GetAllowedActionsAsync(
        Guid caseId,
        string? workflowInstanceId,
        string actorRole,
        CancellationToken cancellationToken);
}
