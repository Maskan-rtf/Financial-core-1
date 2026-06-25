using Core.Application.Common;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using Core.Domain.Identity;


namespace Core.Application.Authorization;

public sealed class CaseAuthorizationService(IUserContext userContext) : ICaseAuthorizationService
{
    #region Permission Maps

    private static readonly string[] InvestmentDepartmentPermissions =
    [
        CasePermissions.ReadAll,
        CasePermissions.ViewInternalComments,
        CasePermissions.CreateInternalComment,
        CasePermissions.ManageValuations,
        CasePermissions.ViewEvaluations,
        CasePermissions.UpsertEvaluations,
        CasePermissions.ManageFinancialWorksheet,
        CasePermissions.UploadDocuments,
        CasePermissions.DownloadDocuments,
        CasePermissions.UploadCommentAttachments
    ];

    private static readonly string[] LegalDepartmentPermissions =
    [
        CasePermissions.ReadAll,
        CasePermissions.ViewInternalComments,
        CasePermissions.CreateInternalComment,
        CasePermissions.ManageContracts,
        CasePermissions.UploadDocuments,
        CasePermissions.DownloadDocuments,
        CasePermissions.UploadCommentAttachments
    ];

    private static readonly string[] FinancialDepartmentPermissions =
    [
        CasePermissions.ReadAll,
        CasePermissions.ViewInternalComments,
        CasePermissions.CreateInternalComment,
        CasePermissions.ManagePayments,
        CasePermissions.ManageFinancialWorksheet,
        CasePermissions.DownloadDocuments,
        CasePermissions.UploadCommentAttachments
    ];

    /// <summary>Every investment-case permission (Admin bypasses the dictionary and does not need this).</summary>
    private static readonly string[] AllCasePermissions =
    [
        CasePermissions.Create,
        CasePermissions.ReadOwn,
        CasePermissions.ReadAll,
        CasePermissions.ViewInternalComments,
        CasePermissions.CreateInternalComment,
        CasePermissions.ViewEvaluations,
        CasePermissions.UpsertEvaluations,
        CasePermissions.ManageValuations,
        CasePermissions.ManageContracts,
        CasePermissions.ManageFinancialWorksheet,
        CasePermissions.ManagePayments,
        CasePermissions.CeoApprove,
        CasePermissions.UploadDocuments,
        CasePermissions.DownloadDocuments,
        CasePermissions.UploadCommentAttachments
    ];

    private static readonly IReadOnlyDictionary<UserDepartment, IReadOnlyCollection<string>> DepartmentPermissions =
        new Dictionary<UserDepartment, IReadOnlyCollection<string>>
        {
            [UserDepartment.Investment] = InvestmentDepartmentPermissions,
            [UserDepartment.Legal] = LegalDepartmentPermissions,
            [UserDepartment.Financial] = FinancialDepartmentPermissions,
            // Preserve the current technical expert access level by keeping the union of expert/manager permissions.
            [UserDepartment.Technical] = AllCasePermissions
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> RolePermissions =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [UserRoleClaims.Applicant] =
            [
                CasePermissions.Create,
                CasePermissions.ReadOwn,
                CasePermissions.UploadDocuments,
                CasePermissions.DownloadDocuments,
                CasePermissions.UploadCommentAttachments
            ],
            [UserRoleClaims.Ceo] =
            [
                CasePermissions.ReadAll,
                CasePermissions.ViewInternalComments,
                CasePermissions.CreateInternalComment,
                CasePermissions.CeoApprove,
                CasePermissions.DownloadDocuments,
                CasePermissions.UploadCommentAttachments
            ]
        };

    #endregion

    #region Public API

    public string? UserId => userContext.UserId;

    public bool IsInternalUser => DepartmentPermissionEvaluator.IsInternalUser(userContext.Roles);

    public Result EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Result.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        return Result.Ok();
    }

    public bool HasPermission(string permission)
    {
        if (userContext.Roles.Contains(UserRoleClaims.Admin))
            return true;

        if (string.Equals(permission, CasePermissions.ReadOwn, StringComparison.OrdinalIgnoreCase))
            return userContext.Roles.Contains(UserRoleClaims.Applicant);

        if (string.Equals(permission, CasePermissions.ReadAll, StringComparison.OrdinalIgnoreCase))
            return IsInternalUser;

        return DepartmentPermissionEvaluator.HasPermission(
            userContext.Roles,
            permission,
            RolePermissions,
            DepartmentPermissions);
    }

    #endregion
}
