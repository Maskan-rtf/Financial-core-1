using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public sealed record LoanKanbanCaseProjection(
    Guid Id,
    string CaseNumber,
    ApplicantType ApplicantType,
    LoanCasePhase CurrentPhase,
    LoanCaseStatus CurrentStatus,
    string? WorkflowInstanceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    decimal? RequestedAmount,
    string? CompanyName,
    string? ApplicantFullName);
