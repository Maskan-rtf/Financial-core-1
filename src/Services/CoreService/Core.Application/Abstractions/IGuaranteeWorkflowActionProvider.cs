using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IGuaranteeWorkflowActionProvider
{
    Task<IReadOnlyCollection<GuaranteeWorkflowAction>> GetAllowedActionsAsync(
        Guid caseId,
        string? workflowInstanceId,
        string actorRole,
        CancellationToken cancellationToken);
}
