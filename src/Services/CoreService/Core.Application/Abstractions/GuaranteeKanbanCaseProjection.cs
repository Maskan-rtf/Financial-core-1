using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public sealed record GuaranteeKanbanCaseProjection(
    Guid Id,
    string CaseNumber,
    ApplicantType ApplicantType,
    GuaranteeCasePhase CurrentPhase,
    GuaranteeCaseStatus CurrentStatus,
    AmendmentType? AmendmentType,
    string? WorkflowInstanceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? RepresentativeName,
    string? CompanyName,
    string? ApplicantFullName);
