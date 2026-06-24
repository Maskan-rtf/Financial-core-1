using Core.Application.Identity.Authorization;
using Core.Domain.Identity;
using Core.Domain.Identity.Entities;


namespace Core.Application.Identity.Services.Authorization;

public class PermissionService : IPermissionService
{
    private readonly IPermissionCacheService _cacheService;

    public PermissionService(IPermissionCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<bool> HasPermissionAsync(User user, string permission, CancellationToken cancellationToken = default)
    {
        if (user.Role == UserRole.Admin)
            return true;

        var userPermissions = await GetUserPermissionsAsync(user, cancellationToken);
        return userPermissions.Contains(permission);
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(User user, CancellationToken cancellationToken = default)
    {
        var cachedPermissions = await _cacheService.GetUserPermissionsAsync(user.Id, cancellationToken);
        if (cachedPermissions != null)
        {
            return cachedPermissions;
        }

        var permissions = await GetRolePermissionsAsync(user.Role.ToString(), cancellationToken);
        var materializedPermissions = permissions as IReadOnlyCollection<string> ?? permissions.ToArray();

        await _cacheService.SetUserPermissionsAsync(user.Id, materializedPermissions, TimeSpan.FromMinutes(30), cancellationToken);

        return materializedPermissions;
    }

    public Task<IEnumerable<string>> GetRolePermissionsAsync(string role, CancellationToken cancellationToken = default)
    {
        role = UserRoleClaims.Normalize(role);
        if (string.Equals(role, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<IEnumerable<string>>(RolePermissions.AllPermissions);

        if (RolePermissions.TryGetPermissionsForRole(role, out var permissions))
        {
            return Task.FromResult<IEnumerable<string>>(permissions);
        }

        return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }
}
