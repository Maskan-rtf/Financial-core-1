using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public sealed record KanbanCaseProjection(
    Guid Id,
    string CaseNumber,
    ApplicantType ApplicantType,
    CasePhase CurrentPhase,
    CaseStatus CurrentStatus,
    string? WorkflowInstanceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? StartupTitle,
    string? CompanyName,
    string? ApplicantFullName);
