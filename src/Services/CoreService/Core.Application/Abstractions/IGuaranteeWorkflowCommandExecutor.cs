using BuildingBlocks.Application.Results;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IGuaranteeWorkflowCommandExecutor
{
    Task<Result> ExecuteAsync(GuaranteeWorkflowExecutionCommand command, CancellationToken cancellationToken);
}

public sealed record GuaranteeWorkflowExecutionCommand(
    Guid CaseId,
    GuaranteeWorkflowAction Action,
    GuaranteeCaseStatus TargetStatus,
    string ActorId,
    string ActorRole,
    Guid CorrelationId,
    string? Comment,
    string? InternalComment);

public sealed record GuaranteeWorkflowCommandPayload(string? InternalComment);
