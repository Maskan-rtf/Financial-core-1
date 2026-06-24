using Core.Domain.Enums;

namespace Core.Application.DTOs;

public sealed record CaseStageRollbackOptionDto(
    int Status,
    string Title,
    int Rank);

public sealed record CaseStageRollbackOptionsDto(
    CaseModuleType Module,
    Guid CaseId,
    int CurrentStatus,
    string CurrentTitle,
    int CurrentRank,
    IReadOnlyList<CaseStageRollbackOptionDto> Options);

public sealed record CaseStageRollbackResultDto(
    CaseModuleType Module,
    Guid CaseId,
    int PreviousStatus,
    int CurrentStatus,
    string CurrentTitle);
