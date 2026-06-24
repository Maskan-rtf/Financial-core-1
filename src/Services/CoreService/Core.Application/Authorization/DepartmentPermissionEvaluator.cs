using Core.Domain.Identity;

namespace Core.Application.Authorization;

internal static class DepartmentPermissionEvaluator
{
    public static bool HasPermission(
        IEnumerable<string> roles,
        string permission,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> rolePermissions,
        IReadOnlyDictionary<UserDepartment, IReadOnlyCollection<string>> departmentPermissions)
    {
        foreach (var role in roles)
        {
            var normalized = UserRoleClaims.Normalize(role);

            if (rolePermissions.TryGetValue(normalized, out var directPermissions) &&
                directPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (UserRoleDepartments.TryGetUserDepartment(normalized, out var department) &&
                departmentPermissions.TryGetValue(department, out var resolvedPermissions) &&
                resolvedPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsInternalUser(IEnumerable<string> roles) =>
        roles.Any(role =>
        {
            var normalized = UserRoleClaims.Normalize(role);

            return string.Equals(normalized, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, UserRoleClaims.Ceo, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "CEO", StringComparison.OrdinalIgnoreCase) ||
                   UserRoleDepartments.TryGetUserDepartment(normalized, out _);
        });
}
