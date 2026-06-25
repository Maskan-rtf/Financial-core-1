using Core.API.Authorization;
using Core.Application.Identity.Authorization;
using Core.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Core.API.DependencyInjection;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddCoreAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", p => p.RequireRole(UserRoleClaims.Admin));
            options.AddPolicy("Users.Delete", p => p.Requirements.Add(new PermissionRequirement(Permissions.Users_Delete)));
            options.AddPolicy("Sessions.Read", p => p.Requirements.Add(new PermissionRequirement(Permissions.Sessions_Read)));
            options.AddPolicy("Sessions.Revoke", p => p.Requirements.Add(new PermissionRequirement(Permissions.Sessions_Revoke)));
            options.AddPolicy("Users.ViewOnline", p => p.Requirements.Add(new PermissionRequirement(Permissions.Users_ViewOnline)));
            options.AddPolicy("ApplicantOnly", p => p.RequireRole(UserRoleClaims.Applicant, UserRoleClaims.Admin));
            options.AddPolicy("InternalOnly", p => p.RequireRole(
                UserRoleClaims.InvestmentExpert,
                UserRoleClaims.InvestmentManager,
                UserRoleClaims.CreditExpert,
                UserRoleClaims.CreditManager,
                UserRoleClaims.LegalExpert,
                UserRoleClaims.LegalManager,
                UserRoleClaims.FinancialExpert,
                UserRoleClaims.FinancialManager,
                UserRoleClaims.TechnicalExpert,
                UserRoleClaims.TechnicalManager,
                UserRoleClaims.Ceo,
                UserRoleClaims.Admin,
                UserRoleClaims.LegalUnit,
                UserRoleClaims.FinancialUnit,
                UserRoleClaims.InvestmentUnit,
                "CEO"));

            options.AddPolicy("FundCreditLimits.Manage", p => p.RequireRole(
                UserRoleClaims.Ceo,
                UserRoleClaims.TechnicalExpert,
                UserRoleClaims.Admin,
                "CEO"));

            options.AddPolicy("GuaranteeCases.CreditReview", p => p.Requirements.Add(new PermissionRequirement("guarantee_cases:credit_review")));
            options.AddPolicy("GuaranteeCases.CeoApprove", p => p.RequireRole(
                UserRoleClaims.Ceo,
                UserRoleClaims.Admin,
                "CEO"));

            options.AddPolicy("GuaranteeCases.CeoOnly", p => p.RequireRole(UserRoleClaims.Ceo, "CEO"));

            options.AddPolicy("LoanCases.CeoApprove", p => p.RequireRole(
                UserRoleClaims.Ceo,
                UserRoleClaims.Admin,
                "CEO"));

            options.AddPolicy("InvestmentCases.Review", p => p.Requirements.Add(new PermissionRequirement("investment_cases:review")));
            options.AddPolicy("InvestmentCases.FinanceReview", p => p.Requirements.Add(new PermissionRequirement("investment_cases:finance_review")));
            options.AddPolicy("InvestmentCases.LegalReview", p => p.Requirements.Add(new PermissionRequirement("investment_cases:legal_review")));
            options.AddPolicy("InvestmentCases.CeoApprove", p => p.RequireRole(
                UserRoleClaims.Ceo,
                UserRoleClaims.Admin,
                "CEO"));

            options.AddPolicy("Dashboard.Ceo", p => p.RequireRole(UserRoleClaims.Ceo, UserRoleClaims.Admin, "CEO"));
            options.AddPolicy("Dashboard.Board", p => p.RequireRole(
                UserRoleClaims.Ceo,
                UserRoleClaims.InvestmentManager,
                UserRoleClaims.Admin,
                "CEO",
                "BoardMember"));
            options.AddPolicy("Dashboard.Executive", p => p.RequireRole(
                UserRoleClaims.Ceo,
                UserRoleClaims.InvestmentManager,
                UserRoleClaims.FinancialManager,
                UserRoleClaims.TechnicalExpert,
                UserRoleClaims.TechnicalManager,
                UserRoleClaims.Admin,
                "CEO",
                "BoardMember"));
            options.AddPolicy("Dashboard.Department", p => p.RequireRole(
                UserRoleClaims.InvestmentExpert,
                UserRoleClaims.InvestmentManager,
                UserRoleClaims.LegalExpert,
                UserRoleClaims.LegalManager,
                UserRoleClaims.FinancialExpert,
                UserRoleClaims.FinancialManager,
                UserRoleClaims.TechnicalExpert,
                UserRoleClaims.TechnicalManager,
                UserRoleClaims.CreditExpert,
                UserRoleClaims.CreditManager,
                UserRoleClaims.Admin,
                UserRoleClaims.LegalUnit,
                UserRoleClaims.FinancialUnit,
                UserRoleClaims.InvestmentUnit));
            options.AddPolicy("Analytics.EmployeeKpi", p => p.RequireRole(
                UserRoleClaims.Ceo,
                UserRoleClaims.TechnicalExpert,
                UserRoleClaims.Admin,
                "CEO",
                "BoardMember"));

            options.AddPolicy("Companies.Delete", p => p.RequireRole(
                UserRoleClaims.Ceo,
                UserRoleClaims.Admin,
                UserRoleClaims.TechnicalExpert,
                "CEO"));

            options.AddPolicy("Companies.Manage", p => p.Requirements.Add(new PermissionRequirement(Permissions.Companies_ListAll)));

            options.AddPolicy("CaseStages.Rollback", p => p.RequireRole(
                UserRoleClaims.Admin,
                UserRoleClaims.TechnicalExpert,
                UserRoleClaims.TechnicalManager));
        });

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
