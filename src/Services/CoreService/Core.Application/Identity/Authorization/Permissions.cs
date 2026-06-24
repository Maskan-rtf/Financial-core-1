using Core.Domain.Identity;

namespace Core.Application.Identity.Authorization;

public static class Permissions
{
    public const string Users_Read = "users:read";
    public const string Users_Write = "users:write";
    public const string Users_Delete = "users:delete";
    public const string Users_ViewOnline = "users:view_online";
    public const string Users_ManageRoles = "users:manage_roles";

    public const string Companies_Read = "companies:read";
    public const string Companies_Write = "companies:write";
    public const string Companies_Delete = "companies:delete";

    public const string Sessions_Read = "sessions:read";
    public const string Sessions_Write = "sessions:write";
    public const string Sessions_Revoke = "sessions:revoke";

    public const string Otp_Send = "otp:send";
    public const string Otp_Verify = "otp:verify";

    public const string Admin_FullAccess = "admin:full_access";

    public const string InvestmentCases_Read = "investment_cases:read";
    public const string InvestmentCases_Write = "investment_cases:write";
    public const string InvestmentCases_Review = "investment_cases:review";
    public const string InvestmentCases_FinanceReview = "investment_cases:finance_review";
    public const string InvestmentCases_LegalReview = "investment_cases:legal_review";
    public const string InvestmentCases_CeoApprove = "investment_cases:ceo_approve";

    public const string GuaranteeCases_Read = "guarantee_cases:read";
    public const string GuaranteeCases_Write = "guarantee_cases:write";
    public const string GuaranteeCases_CreditReview = "guarantee_cases:credit_review";
    public const string GuaranteeCases_LegalReview = "guarantee_cases:legal_review";
    public const string GuaranteeCases_FinanceReview = "guarantee_cases:finance_review";
    public const string GuaranteeCases_CeoApprove = "guarantee_cases:ceo_approve";
    public const string GuaranteeCases_SetApplicantCreditLimit = "guarantee_cases:set_applicant_credit_limit";
}

public static class RolePermissions
{
    /// <summary>Every API permission string — assigned to <see cref="UserRoleClaims.Admin"/>.</summary>
    public static readonly IReadOnlyCollection<string> AllPermissions =
    [
        Permissions.Users_Read,
        Permissions.Users_Write,
        Permissions.Users_Delete,
        Permissions.Users_ViewOnline,
        Permissions.Users_ManageRoles,
        Permissions.Companies_Read,
        Permissions.Companies_Write,
        Permissions.Companies_Delete,
        Permissions.Sessions_Read,
        Permissions.Sessions_Write,
        Permissions.Sessions_Revoke,
        Permissions.Otp_Send,
        Permissions.Otp_Verify,
        Permissions.Admin_FullAccess,
        Permissions.InvestmentCases_Read,
        Permissions.InvestmentCases_Write,
        Permissions.InvestmentCases_Review,
        Permissions.InvestmentCases_FinanceReview,
        Permissions.InvestmentCases_LegalReview,
        Permissions.InvestmentCases_CeoApprove,
        Permissions.GuaranteeCases_Read,
        Permissions.GuaranteeCases_Write,
        Permissions.GuaranteeCases_CreditReview,
        Permissions.GuaranteeCases_LegalReview,
        Permissions.GuaranteeCases_FinanceReview,
        Permissions.GuaranteeCases_CeoApprove,
        Permissions.GuaranteeCases_SetApplicantCreditLimit
    ];

    private static readonly string[] LegalDepartmentPermissions =
    [
        Permissions.Users_Read,
        Permissions.Companies_Read,
        Permissions.Sessions_Read,
        Permissions.InvestmentCases_Read,
        Permissions.InvestmentCases_LegalReview,
        Permissions.GuaranteeCases_Read,
        Permissions.GuaranteeCases_LegalReview
    ];

    private static readonly string[] FinancialDepartmentPermissions =
    [
        Permissions.Users_Read,
        Permissions.Companies_Read,
        Permissions.Companies_Write,
        Permissions.Sessions_Read,
        Permissions.InvestmentCases_Read,
        Permissions.InvestmentCases_FinanceReview,
        Permissions.GuaranteeCases_Read,
        Permissions.GuaranteeCases_FinanceReview
    ];

    private static readonly string[] InvestmentDepartmentPermissions =
    [
        Permissions.Users_Read,
        Permissions.Users_Write,
        Permissions.Companies_Read,
        Permissions.Companies_Write,
        Permissions.Sessions_Read,
        Permissions.Otp_Send,
        Permissions.Otp_Verify,
        Permissions.InvestmentCases_Read,
        Permissions.InvestmentCases_Review,
        Permissions.InvestmentCases_Write
    ];

    private static readonly string[] CreditDepartmentPermissions =
    [
        Permissions.Users_Read,
        Permissions.Companies_Read,
        Permissions.Companies_Write,
        Permissions.Sessions_Read,
        Permissions.GuaranteeCases_Read,
        Permissions.GuaranteeCases_CreditReview,
        Permissions.GuaranteeCases_Write
    ];

    public static readonly IReadOnlyDictionary<UserDepartment, IReadOnlyCollection<string>> DepartmentPermissionMappings =
        new Dictionary<UserDepartment, IReadOnlyCollection<string>>
        {
            [UserDepartment.Investment] = InvestmentDepartmentPermissions,
            [UserDepartment.Credit] = CreditDepartmentPermissions,
            [UserDepartment.Legal] = LegalDepartmentPermissions,
            [UserDepartment.Financial] = FinancialDepartmentPermissions,
            // Keep the current broad technical access by preserving the union of existing expert/manager capabilities.
            [UserDepartment.Technical] = AllPermissions
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> RolePermissionMappings =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [UserRoleClaims.Admin] = AllPermissions,
            [UserRoleClaims.Applicant] =
            [
                Permissions.Users_Read,
                Permissions.Companies_Read,
                Permissions.Companies_Write,
                Permissions.Sessions_Read,
                Permissions.Otp_Send,
                Permissions.Otp_Verify,
                Permissions.GuaranteeCases_Read,
                Permissions.GuaranteeCases_Write
            ],
            [UserRoleClaims.Ceo] =
            [
                Permissions.Users_Read,
                Permissions.Users_Delete,
                Permissions.Users_ViewOnline,
                Permissions.Companies_Read,
                Permissions.Sessions_Read,
                Permissions.Sessions_Revoke,
                Permissions.InvestmentCases_Read,
                Permissions.InvestmentCases_CeoApprove,
                Permissions.GuaranteeCases_Read,
                Permissions.GuaranteeCases_CeoApprove,
                Permissions.GuaranteeCases_SetApplicantCreditLimit
            ]
        };

    public static bool TryGetPermissionsForRole(string role, out IReadOnlyCollection<string> permissions)
    {
        var normalized = UserRoleClaims.Normalize(role);

        if (RolePermissionMappings.TryGetValue(normalized, out var directPermissions))
        {
            permissions = directPermissions;
            return true;
        }

        if (UserRoleDepartments.TryGetUserDepartment(normalized, out var department) &&
            DepartmentPermissionMappings.TryGetValue(department, out var departmentPermissions))
        {
            permissions = departmentPermissions;
            return true;
        }

        permissions = Array.Empty<string>();
        return false;
    }
}
