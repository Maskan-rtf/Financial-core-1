using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public sealed record LoanCaseCommentListProjection(
    Guid Id,
    LoanCasePhase Phase,
    string SenderUserId,
    string? SenderRole,
    string Message,
    bool IsRevisionRequest,
    bool IsInternal,
    Guid? ParentId,
    DateTimeOffset CreatedAt,
    string? SenderFullName,
    LoanCaseStatus? WorkflowStatusAtCreation);
