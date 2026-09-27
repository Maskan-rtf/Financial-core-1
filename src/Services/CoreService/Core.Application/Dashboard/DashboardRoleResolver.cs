using Core.Domain.Identity;

namespace Core.Application.Dashboard;

public enum DashboardViewKind
{
    Executive,
    Department,
    Applicant
}

public static class DashboardRoleResolver
{
    public const string ExecutiveSnapshotKey = "executive:global";

    public static readonly IReadOnlyList<string> DepartmentKeys = ["Legal", "Financial", "Credit", "Investment", "Technical"];

    private static readonly Dictionary<string, (string Title, string RepresentativeRole)> DepartmentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Legal"] = ("حقوقی", UserRoleClaims.LegalExpert),
        ["Financial"] = ("مالی", UserRoleClaims.FinancialExpert),
        ["Credit"] = ("اعتبارات", UserRoleClaims.CreditExpert),
        ["Investment"] = ("سرمایه‌گذاری", UserRoleClaims.InvestmentExpert),
        ["Technical"] = ("فنی", UserRoleClaims.TechnicalExpert)
    };

    public static string DepartmentSnapshotKey(string departmentKey) => $"department:{departmentKey}";

    public static string ApplicantSnapshotKey(string userId) => $"applicant:{userId}";

    public static DashboardViewKind ResolveViewKind(IReadOnlyCollection<string> roles)
    {
        if (roles.Count == 0)
            return DashboardViewKind.Applicant;

        var normalized = roles.Select(UserRoleClaims.Normalize).ToArray();

        if (normalized.Any(r =>
                r.Equals(UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase) ||
                r.Equals(UserRoleClaims.TechnicalExpert, StringComparison.OrdinalIgnoreCase) ||
                r.Equals(UserRoleClaims.TechnicalManager, StringComparison.OrdinalIgnoreCase)))
            return DashboardViewKind.Executive;

        if (normalized.Any(r =>
                r.Equals(UserRoleClaims.Ceo, StringComparison.OrdinalIgnoreCase) ||
                r.Equals("CEO", StringComparison.OrdinalIgnoreCase)))
            return DashboardViewKind.Executive;

        if (normalized.Any(r => r.Equals(UserRoleClaims.BoardMember, StringComparison.OrdinalIgnoreCase)))
            return DashboardViewKind.Executive;

        if (normalized.Any(r =>
                r.Equals(UserRoleClaims.InvestmentManager, StringComparison.OrdinalIgnoreCase) ||
                r.Equals(UserRoleClaims.FinancialManager, StringComparison.OrdinalIgnoreCase)))
            return DashboardViewKind.Executive;

        if (normalized.Any(r => r.Equals(UserRoleClaims.Applicant, StringComparison.OrdinalIgnoreCase)) &&
            !normalized.Any(IsInternalRole))
            return DashboardViewKind.Applicant;

        if (normalized.Any(IsInternalRole))
            return DashboardViewKind.Department;

        return DashboardViewKind.Applicant;
    }

    public static bool IsAdmin(IReadOnlyCollection<string> roles)
        => roles.Select(UserRoleClaims.Normalize).Any(r =>
            r.Equals(UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase));

    public static bool IsBoardMember(IReadOnlyCollection<string> roles)
        => roles.Select(UserRoleClaims.Normalize).Any(r =>
            r.Equals(UserRoleClaims.BoardMember, StringComparison.OrdinalIgnoreCase));

    public static bool CanViewEmployeeKpi(IReadOnlyCollection<string> roles)
    {
        var normalized = roles.Select(UserRoleClaims.Normalize).ToArray();
        return normalized.Any(r =>
            r.Equals(UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoleClaims.Ceo, StringComparison.OrdinalIgnoreCase) ||
            r.Equals("CEO", StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoleClaims.BoardMember, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoleClaims.TechnicalExpert, StringComparison.OrdinalIgnoreCase));
    }

    public static string? ResolveDepartmentKey(IReadOnlyCollection<string> roles)
    {
        foreach (var role in roles.Select(UserRoleClaims.Normalize))
        {
            var key = ResolveDepartmentKeyFromRole(role);
            if (key is not null)
                return key;
        }

        return null;
    }

    public static string ResolvePrimaryRole(IReadOnlyCollection<string> roles)
    {
        foreach (var role in roles.Select(UserRoleClaims.Normalize))
        {
            if (!string.IsNullOrWhiteSpace(role))
                return role;
        }

        return UserRoleClaims.Applicant;
    }

    public static string GetDepartmentTitle(string departmentKey)
        => DepartmentMap.TryGetValue(departmentKey, out var meta) ? meta.Title : departmentKey;

    public static string GetDepartmentRepresentativeRole(string departmentKey)
        => DepartmentMap.TryGetValue(departmentKey, out var meta) ? meta.RepresentativeRole : UserRoleClaims.Admin;

    public static string ResolveDepartmentKeyFromActorRole(string? actorRole)
    {
        if (string.IsNullOrWhiteSpace(actorRole))
            return "Other";

        actorRole = UserRoleClaims.Normalize(actorRole);

        if (actorRole.Equals(UserRoleClaims.Ceo, StringComparison.OrdinalIgnoreCase) ||
            actorRole.Equals("CEO", StringComparison.OrdinalIgnoreCase) ||
            actorRole.Equals(UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
            return "Management";

        return ResolveDepartmentKeyFromRole(actorRole) ?? "Other";
    }

    public static string GetDepartmentTitleFromActorRole(string? actorRole)
    {
        var key = ResolveDepartmentKeyFromActorRole(actorRole);
        return key switch
        {
            "Management" => "مدیریت",
            "Other" => "سایر",
            _ => GetDepartmentTitle(key)
        };
    }

    public static bool IsInternalActorRole(string? actorRole)
    {
        if (string.IsNullOrWhiteSpace(actorRole))
            return false;

        actorRole = UserRoleClaims.Normalize(actorRole);
        return !actorRole.Equals(UserRoleClaims.Applicant, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveDepartmentKeyFromRole(string role)
    {
        role = UserRoleClaims.Normalize(role);

        if (role.StartsWith("Legal", StringComparison.OrdinalIgnoreCase))
            return "Legal";
        if (role.StartsWith("Financial", StringComparison.OrdinalIgnoreCase))
            return "Financial";
        if (role.StartsWith("Credit", StringComparison.OrdinalIgnoreCase))
            return "Credit";
        if (role.StartsWith("Investment", StringComparison.OrdinalIgnoreCase))
            return "Investment";
        if (role.StartsWith("Technical", StringComparison.OrdinalIgnoreCase))
            return "Technical";

        return null;
    }

    private static bool IsInternalRole(string role)
    {
        role = UserRoleClaims.Normalize(role);
        return role is not (UserRoleClaims.Applicant);
    }
}
