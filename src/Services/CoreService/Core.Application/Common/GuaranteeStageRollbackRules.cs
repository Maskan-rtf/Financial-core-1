using Core.Domain.Enums;

namespace Core.Application.Common;

internal static class GuaranteeStageRollbackRules
{
    public static HashSet<int> ExpandVisitedStatuses(
        int currentStatus,
        AmendmentType? amendmentType,
        HashSet<int> visited)
    {
        if (currentStatus < (int)GuaranteeCaseStatus.AmendmentDraft)
            return visited;

        var enteredAmendment =
            visited.Contains((int)GuaranteeCaseStatus.Completed) ||
            visited.Any(status => status >= (int)GuaranteeCaseStatus.AmendmentDraft);

        if (!enteredAmendment)
            return visited;

        AddIssuanceSpineStatuses(visited);

        foreach (var status in GetAmendmentSpineStatuses(currentStatus))
            visited.Add(status);

        return visited;
    }

    private static void AddIssuanceSpineStatuses(HashSet<int> visited)
    {
        for (var status = (int)GuaranteeCaseStatus.Draft; status <= (int)GuaranteeCaseStatus.Completed; status++)
        {
            if (!Enum.IsDefined(typeof(GuaranteeCaseStatus), status))
                continue;

            visited.Add(status);
        }
    }

    private static IEnumerable<int> GetAmendmentSpineStatuses(int currentStatus)
    {
        if (currentStatus <= (int)GuaranteeCaseStatus.AmendmentDraft)
            yield break;

        var upperBound = Math.Min(currentStatus - 1, (int)GuaranteeCaseStatus.AmendmentApproved);

        for (var status = (int)GuaranteeCaseStatus.AmendmentDraft; status <= upperBound; status++)
        {
            if (!Enum.IsDefined(typeof(GuaranteeCaseStatus), status))
                continue;

            if (status is (int)GuaranteeCaseStatus.AmendmentCompleted
                or (int)GuaranteeCaseStatus.Rejected
                or (int)GuaranteeCaseStatus.Cancelled
                or (int)GuaranteeCaseStatus.Archived)
                continue;

            yield return status;
        }
    }
}
