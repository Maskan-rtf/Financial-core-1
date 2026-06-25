using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public sealed record GuaranteeCaseCommentListProjection(
    Guid Id,
    GuaranteeCasePhase Phase,
    string SenderUserId,
    string? SenderRole,
    string Message,
    bool IsRevisionRequest,
    bool IsInternal,
    DateTimeOffset CreatedAt,
    string? SenderFullName,
    GuaranteeCaseStatus? WorkflowStatusAtCreation);
