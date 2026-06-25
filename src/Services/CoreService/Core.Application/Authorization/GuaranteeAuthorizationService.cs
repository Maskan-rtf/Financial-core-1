using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using Core.Application.Common;
using Core.Domain.Identity;

namespace Core.Application.Authorization;

public sealed class GuaranteeAuthorizationService(IUserContext userContext) : IGuaranteeAuthorizationService
{
    #region Permission Maps

    private static readonly string[] CreditDepartmentPermissions =
    [
        GuaranteePermissions.ReadAll,
        GuaranteePermissions.ViewInternalComments,
        GuaranteePermissions.CreateInternalComment,
        GuaranteePermissions.ManageApprovalForm,
        GuaranteePermissions.DownloadDocuments
    ];

    private static readonly string[] LegalDepartmentPermissions =
    [
        GuaranteePermissions.ReadAll,
        GuaranteePermissions.ViewInternalComments,
        GuaranteePermissions.CreateInternalComment,
        GuaranteePermissions.ManageContracts,
        GuaranteePermissions.UploadDocuments,
        GuaranteePermissions.DownloadDocuments
    ];

    private static readonly string[] FinancialDepartmentPermissions =
    [
        GuaranteePermissions.ReadAll,
        GuaranteePermissions.ViewInternalComments,
        GuaranteePermissions.CreateInternalComment,
        GuaranteePermissions.ManageAttachments,
        GuaranteePermissions.ManageIssuance,
        GuaranteePermissions.UploadDocuments,
        GuaranteePermissions.DownloadDocuments
    ];

    /// <summary>Every guarantee-case permission (Admin uses this list explicitly).</summary>
    private static readonly string[] AllGuaranteePermissions =
    [
        GuaranteePermissions.Create,
        GuaranteePermissions.ReadAll,
        GuaranteePermissions.ReadOwn,
        GuaranteePermissions.ViewInternalComments,
        GuaranteePermissions.CreateInternalComment,
        GuaranteePermissions.ManageApprovalForm,
        GuaranteePermissions.ManageContracts,
        GuaranteePermissions.ManageAttachments,
        GuaranteePermissions.ManageIssuance,
        GuaranteePermissions.CeoApprove,
        GuaranteePermissions.SetApplicantCreditLimit,
        GuaranteePermissions.UploadDocuments,
        GuaranteePermissions.DownloadDocuments
    ];

    private static readonly IReadOnlyDictionary<UserDepartment, IReadOnlyCollection<string>> DepartmentPermissions =
        new Dictionary<UserDepartment, IReadOnlyCollection<string>>
        {
            [UserDepartment.Credit] = CreditDepartmentPermissions,
            [UserDepartment.Legal] = LegalDepartmentPermissions,
            [UserDepartment.Financial] = FinancialDepartmentPermissions,
            [UserDepartment.Technical] = AllGuaranteePermissions
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> RolePermissions =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [UserRoleClaims.Applicant] =
            [
                GuaranteePermissions.Create,
                GuaranteePermissions.ReadOwn,
                GuaranteePermissions.UploadDocuments,
                GuaranteePermissions.DownloadDocuments
            ],
            [UserRoleClaims.Ceo] =
            [
                GuaranteePermissions.ReadAll,
                GuaranteePermissions.CeoApprove,
                GuaranteePermissions.SetApplicantCreditLimit
            ]
        };

    #endregion

    #region Public API

    public string? UserId => userContext.UserId;

    public bool IsInternalUser => DepartmentPermissionEvaluator.IsInternalUser(userContext.Roles);

    public Result<string> EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result<string>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        return Result<string>.Ok(userContext.UserId);
    }

    public bool HasPermission(string permission)
    {
        if (userContext.Roles.Contains(UserRoleClaims.Admin))
            return true;

        return DepartmentPermissionEvaluator.HasPermission(
            userContext.Roles,
            permission,
            RolePermissions,
            DepartmentPermissions);
    }

    #endregion
}
