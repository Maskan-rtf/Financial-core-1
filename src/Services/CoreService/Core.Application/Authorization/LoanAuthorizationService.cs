using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using Core.Application.Common;
using Core.Domain.Identity;

namespace Core.Application.Authorization;

public sealed class LoanAuthorizationService(IUserContext userContext) : ILoanAuthorizationService
{
    private static readonly string[] CreditDepartmentPermissions =
    [
        LoanPermissions.ReadAll,
        LoanPermissions.ViewInternalComments,
        LoanPermissions.CreateInternalComment,
        LoanPermissions.ManageApprovalDetail,
        LoanPermissions.DownloadDocuments
    ];

    private static readonly string[] LegalDepartmentPermissions =
    [
        LoanPermissions.ReadAll,
        LoanPermissions.ViewInternalComments,
        LoanPermissions.CreateInternalComment,
        LoanPermissions.ManageContracts,
        LoanPermissions.ManageInstallments,
        LoanPermissions.UploadDocuments,
        LoanPermissions.DownloadDocuments
    ];

    private static readonly string[] FinancialDepartmentPermissions =
    [
        LoanPermissions.ReadAll,
        LoanPermissions.ViewInternalComments,
        LoanPermissions.CreateInternalComment,
        LoanPermissions.ManagePayments,
        LoanPermissions.UploadDocuments,
        LoanPermissions.DownloadDocuments
    ];

    private static readonly string[] AllLoanPermissions =
    [
        LoanPermissions.Create,
        LoanPermissions.ReadAll,
        LoanPermissions.ReadOwn,
        LoanPermissions.ViewInternalComments,
        LoanPermissions.CreateInternalComment,
        LoanPermissions.ManageApprovalDetail,
        LoanPermissions.ManageContracts,
        LoanPermissions.ManageInstallments,
        LoanPermissions.ManagePayments,
        LoanPermissions.RepayInstallments,
        LoanPermissions.CeoApprove,
        LoanPermissions.UploadDocuments,
        LoanPermissions.DownloadDocuments
    ];

    private static readonly IReadOnlyDictionary<UserDepartment, IReadOnlyCollection<string>> DepartmentPermissions =
        new Dictionary<UserDepartment, IReadOnlyCollection<string>>
        {
            [UserDepartment.Credit] = CreditDepartmentPermissions,
            [UserDepartment.Legal] = LegalDepartmentPermissions,
            [UserDepartment.Financial] = FinancialDepartmentPermissions,
            [UserDepartment.Technical] = AllLoanPermissions
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> RolePermissions =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [UserRoleClaims.Applicant] =
            [
                LoanPermissions.Create,
                LoanPermissions.ReadOwn,
                LoanPermissions.RepayInstallments,
                LoanPermissions.UploadDocuments,
                LoanPermissions.DownloadDocuments
            ],
            [UserRoleClaims.Ceo] =
            [
                LoanPermissions.ReadAll,
                LoanPermissions.ViewInternalComments,
                LoanPermissions.DownloadDocuments,
                LoanPermissions.CeoApprove
            ]
        };

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
}
