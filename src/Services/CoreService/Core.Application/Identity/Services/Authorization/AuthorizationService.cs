using Core.Application.Abstractions;
using Core.Application.Identity.Authorization;
using Core.Domain.Identity.Entities;


namespace Core.Application.Identity.Services.Authorization;

public class AuthorizationService : IAuthorizationService
{
    private readonly IPermissionService _permissionService;
    private readonly ICoreUnitOfWork _unitOfWork;

    public AuthorizationService(
        IPermissionService permissionService,
        ICoreUnitOfWork unitOfWork)
    {
        _permissionService = permissionService;
        _unitOfWork = unitOfWork;
    }

    #region Public API

    public async Task<bool> AuthorizeAsync(User user, string permission, CancellationToken cancellationToken = default)
    {
        return await _permissionService.HasPermissionAsync(user, permission, cancellationToken);
    }

    public async Task<bool> AuthorizeAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetAsync(u => u.Id == userId);
        if (user == null)
        {
            return false;
        }

        return await _permissionService.HasPermissionAsync(user, permission, cancellationToken);
    }

    #endregion
}