using BuildingBlocks.Application.Results;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface ILoanWorkflowCommandExecutor
{
    Task<Result> ExecuteAsync(LoanWorkflowExecutionCommand command, CancellationToken cancellationToken);
}

public sealed record LoanWorkflowExecutionCommand(
    Guid CaseId,
    LoanWorkflowAction Action,
    LoanCaseStatus TargetStatus,
    string ActorId,
    string ActorRole,
    Guid CorrelationId,
    string? Comment,
    string? InternalComment);

public sealed record LoanWorkflowCommandPayload(string? InternalComment);
