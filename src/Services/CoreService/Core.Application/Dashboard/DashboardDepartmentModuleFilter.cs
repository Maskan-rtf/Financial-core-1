namespace Core.Application.Dashboard;

/// <summary>
/// Controls which case modules appear on department dashboards and reports.
/// </summary>
public static class DashboardDepartmentModuleFilter
{
    private static readonly Dictionary<string, HashSet<string>> HiddenModulesByDepartment =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Credit"] = new(StringComparer.OrdinalIgnoreCase) { "Investment" },
            ["Investment"] = new(StringComparer.OrdinalIgnoreCase) { "Guarantee", "Loan", "GuaranteeRenewal" }
        };

    public static bool IsModuleVisible(string? departmentKey, string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(departmentKey))
            return true;

        return !HiddenModulesByDepartment.TryGetValue(departmentKey, out var hidden) ||
               !hidden.Contains(moduleKey);
    }

    public static IReadOnlyList<ModuleDashboardMetricsDto> FilterModules(
        string? departmentKey,
        IReadOnlyList<ModuleDashboardMetricsDto> modules)
    {
        if (string.IsNullOrWhiteSpace(departmentKey))
            return modules;

        return modules.Where(m => IsModuleVisible(departmentKey, m.Module)).ToList();
    }

    public static IReadOnlyList<ModuleQueueCountDto> FilterQueue(
        string? departmentKey,
        IReadOnlyList<ModuleQueueCountDto> queue)
    {
        if (string.IsNullOrWhiteSpace(departmentKey))
            return queue;

        return queue.Where(q => IsModuleVisible(departmentKey, q.Module)).ToList();
    }
}
