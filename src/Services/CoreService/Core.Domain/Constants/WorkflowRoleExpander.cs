using Core.Domain.Enums;

namespace Core.Domain.Constants;

/// <summary>Mirrors kanban ownership from unit expert roles to manager roles.</summary>
public static class WorkflowRoleExpander
{
    public static void MirrorKanbanRole(
        IDictionary<string, HashSet<CaseStatus>> map,
        string fromRole,
        string toRole)
    {
        MergeKanbanRoleStatuses(map, fromRole, toRole);
    }

    public static void MirrorKanbanRole(
        IDictionary<string, HashSet<GuaranteeCaseStatus>> map,
        string fromRole,
        string toRole)
    {
        MergeKanbanRoleStatuses(map, fromRole, toRole);
    }

    public static void MirrorKanbanRole(
        IDictionary<string, HashSet<LoanCaseStatus>> map,
        string fromRole,
        string toRole)
    {
        MergeKanbanRoleStatuses(map, fromRole, toRole);
    }

    private static void MergeKanbanRoleStatuses<TStatus>(
        IDictionary<string, HashSet<TStatus>> map,
        string fromRole,
        string toRole)
    {
        if (!map.TryGetValue(fromRole, out var statuses))
            return;

        if (!map.TryGetValue(toRole, out var target))
        {
            map[toRole] = statuses.ToHashSet();
            return;
        }

        foreach (var status in statuses)
            target.Add(status);
    }
}
