using Core.Domain.Enums;

namespace Core.Application.DTOs;

public sealed record KanbanCaseCardDto(
    Guid Id,
    string CaseNumber,
    CaseModuleType Module,
    string ApiBasePath,
    string StatusKey,
    int StatusValue,
    string PhaseTitle,
    string StatusTitle,
    ApplicantType? ApplicantType,
    string? StartupTitle,
    string? CompanyName,
    string? ApplicantFullName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string PendingActionLabel,
    IReadOnlyList<string> AllowedActions);

public sealed record KanbanCaseSummaryDto(
    Guid Id,
    string CaseNumber,
    CaseModuleType Module,
    string ApiBasePath,
    string StatusKey,
    int StatusValue,
    string PhaseTitle,
    string StatusTitle,
    string? StartupTitle,
    DateTimeOffset CreatedAt,
    string PendingActionLabel,
    string? CompanyName = null,
    string? ApplicantFullName = null,
    ApplicantType? ApplicantType = null,
    DateTimeOffset? UpdatedAt = null);
