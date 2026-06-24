namespace Core.Application.Dashboard;

internal static class EmployeeKpiDepartmentFilter
{
    private static readonly HashSet<string> ExcludedDepartmentKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Management",
    };

    public static bool IncludeInSlaDashboard(string departmentKey)
        => !ExcludedDepartmentKeys.Contains(departmentKey);

    public static IReadOnlyList<DepartmentEmployeeKpiDto> FilterForSlaDashboard(
        IReadOnlyList<DepartmentEmployeeKpiDto> departments)
        => departments.Where(d => IncludeInSlaDashboard(d.DepartmentKey)).ToList();
}
