using BuildingBlocks.Application.Results;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IInvestmentWorkflowCoordinator
{
    Task<Result> ApplyTransitionAsync(
        InvestmentWorkflowTransitionRequest request,
        CancellationToken cancellationToken);
}

public sealed record InvestmentWorkflowTransitionRequest(
    Guid CaseId,
    WorkflowAction Action,
    string ActorId,
    string ActorRole,
    bool IsInternalUser,
    string? Comment = null,
    string? InternalComment = null);
