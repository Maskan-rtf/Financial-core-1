using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IInvestmentWorkflowActionProvider
{
    Task<IReadOnlyCollection<WorkflowAction>> GetAllowedActionsAsync(
        Guid caseId,
        string? workflowInstanceId,
        string actorRole,
        CancellationToken cancellationToken);
}
