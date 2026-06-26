using BuildingBlocks.Application.Results;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IInvestmentWorkflowCommandExecutor
{
    Task<Result> ExecuteAsync(InvestmentWorkflowExecutionCommand command, CancellationToken cancellationToken);
}

public sealed record InvestmentWorkflowExecutionCommand(
    Guid CaseId,
    WorkflowAction Action,
    CaseStatus TargetStatus,
    string ActorId,
    string ActorRole,
    Guid CorrelationId,
    string? Comment,
    string? InternalComment);

public sealed record InvestmentWorkflowCommandPayload(string? InternalComment);
