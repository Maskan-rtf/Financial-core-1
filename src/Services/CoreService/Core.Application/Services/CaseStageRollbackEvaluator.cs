using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Common;
using Core.Application.DTOs;
using Core.Domain.Enums;

namespace Core.Application.Services;

internal static class CaseStageRollbackEvaluator
{
    public static Result Validate(
        CaseModuleType module,
        int currentStatus,
        int targetStatus,
        IReadOnlyCollection<int> visitedStatuses,
        AmendmentType? guaranteeAmendmentType = null)
    {
        if (targetStatus == currentStatus)
            return Result.Fail(Error.Validation(ApiMessages.CannotRollbackToSameStage));

        if (!CaseStageOrder.TryGetRank(module, targetStatus, out var targetRank))
            return Result.Fail(Error.Validation(ApiMessages.InvalidTargetStage));

        if (!CaseStageOrder.TryGetRank(module, currentStatus, out var currentRank))
            return Result.Fail(Error.Validation(ApiMessages.InvalidTargetStage));

        if (targetRank >= currentRank)
            return Result.Fail(Error.Validation(ApiMessages.CannotAdvanceCaseStage));

        var expandedVisited = visitedStatuses as HashSet<int> ?? visitedStatuses.ToHashSet();
        if (module == CaseModuleType.Guarantee)
            expandedVisited = GuaranteeStageRollbackRules.ExpandVisitedStatuses(currentStatus, guaranteeAmendmentType, expandedVisited);

        if (!expandedVisited.Contains(targetStatus))
            return Result.Fail(Error.Validation(ApiMessages.TargetStageNotVisited));

        return Result.Ok();
    }

    public static HashSet<int> CollectVisitedStatuses(
        CaseModuleType module,
        int currentStatus,
        IEnumerable<(int FromStatus, int ToStatus)> historyEntries,
        AmendmentType? guaranteeAmendmentType = null)
    {
        var visited = new HashSet<int> { currentStatus };

        foreach (var (fromStatus, toStatus) in historyEntries)
        {
            visited.Add(fromStatus);
            visited.Add(toStatus);
        }

        if (module == CaseModuleType.Guarantee)
            visited = GuaranteeStageRollbackRules.ExpandVisitedStatuses(currentStatus, guaranteeAmendmentType, visited);

        return visited;
    }

    public static IReadOnlyList<CaseStageRollbackOptionDto> BuildOptions(
        CaseModuleType module,
        int currentStatus,
        IReadOnlyCollection<int> visitedStatuses)
    {
        if (!CaseStageOrder.TryGetRank(module, currentStatus, out var currentRank))
            return [];

        return visitedStatuses
            .Where(status => status != currentStatus)
            .Where(status => CaseStageOrder.TryGetRank(module, status, out var rank) && rank < currentRank)
            .Select(status => new CaseStageRollbackOptionDto(
                status,
                CaseStageOrder.GetTitle(module, status),
                CaseStageOrder.TryGetRank(module, status, out var rank) ? rank : 0))
            .OrderByDescending(x => x.Rank)
            .ToList();
    }
}
