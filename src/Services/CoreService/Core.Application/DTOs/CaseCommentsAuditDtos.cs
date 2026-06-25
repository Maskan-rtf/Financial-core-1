namespace Core.Application.DTOs;

public enum AuditedCaseType
{
    Investment = 1,
    Loan = 2,
    Guarantee = 3
}

public sealed record CaseCommentAuditAttachmentDto(
    Guid Id,
    string? S3Key,
    string FileName);

public sealed record CaseCommentAuditItemDto(
    Guid Id,
    AuditedCaseType CaseType,
    Guid CaseId,
    string? CaseNumber,
    string? CaseTitle,
    int Phase,
    string PhaseLabel,
    string SenderUserId,
    string? SenderFullName,
    string? SenderRole,
    string Message,
    bool IsRevisionRequest,
    bool IsInternal,
    Guid? ParentId,
    IReadOnlyList<CaseCommentAuditAttachmentDto> Attachments,
    DateTimeOffset CreatedAt,
    int? WorkflowStatusAtCreation = null,
    string? WorkflowStatusLabel = null);

public sealed record CaseCommentsAuditListResult(
    IReadOnlyList<CaseCommentAuditItemDto> Items,
    int Skip,
    int Take,
    int TotalCount);
