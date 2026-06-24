using System.Security.Claims;
using Core.Application.Abstractions;
using Core.Application.Identity.Authorization;
using Core.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Core.API.Authorization;

public sealed class PermissionAuthorizationHandler(IIdentityClient identityClient, ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (TrySucceedFromRoleClaims(context, requirement))
            return;

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return;

        try
        {
            var allowed = await identityClient.ValidateUserPermissionAsync(userId, requirement.Permission, CancellationToken.None);
            if (allowed)
                context.Succeed(requirement);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Permission validation via IdentityService failed.");
        }
    }

    private static bool TrySucceedFromRoleClaims(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roles.Count == 0)
            return false;

        if (roles.Contains(UserRoleClaims.Admin))
        {
            context.Succeed(requirement);
            return true;
        }

        if (string.Equals(requirement.Permission, Permissions.InvestmentCases_CeoApprove, StringComparison.OrdinalIgnoreCase))
        {
            if (roles.Contains(UserRoleClaims.Ceo) || roles.Contains("CEO") || roles.Contains(UserRoleClaims.Admin))
            {
                context.Succeed(requirement);
                return true;
            }
        }

        foreach (var role in roles)
        {
            if (!RolePermissions.TryGetPermissionsForRole(role, out var permissions))
                continue;

            if (permissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
                return true;
            }
        }

        return false;
    }
}
