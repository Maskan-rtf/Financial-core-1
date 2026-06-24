using Core.Domain.Identity;

namespace Core.Application.Common;

public static class CaseStageRollbackAuthorization
{
    private static readonly HashSet<string> AuthorizedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        UserRoleClaims.Admin,
        UserRoleClaims.TechnicalExpert,
        UserRoleClaims.TechnicalManager
    };

    public static bool CanRollbackStage(IEnumerable<string> roles)
        => roles.Any(r => AuthorizedRoles.Contains(UserRoleClaims.Normalize(r)));
}
