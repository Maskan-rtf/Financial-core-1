using Core.Application.Abstractions;
using Core.Application.Authorization;
using Core.Application.Services;
using FluentValidation;
using FluentValidation.AspNetCore;

namespace Core.API.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCoreApiServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<InvestmentCaseAppService>();
        services.AddFluentValidationAutoValidation();

        services.AddScoped<ICaseStateManager, CaseStateManager>();
        services.AddScoped<IInvestmentWorkflowCoordinator, InvestmentWorkflowCoordinator>();
        services.AddScoped<IInvestmentCaseAppService, InvestmentCaseAppService>();
        services.AddScoped<IGuaranteeCaseStateManager, GuaranteeCaseStateManager>();
        services.AddScoped<IGuaranteeCaseAppService, GuaranteeCaseAppService>();
        services.AddScoped<IGuaranteeRenewalAppService, GuaranteeRenewalAppService>();
        services.AddScoped<ILoanCaseStateManager, LoanCaseStateManager>();
        services.AddScoped<IFundCreditLimitAppService, FundCreditLimitAppService>();
        services.AddScoped<ILoanCaseAppService, LoanCaseAppService>();
        services.AddScoped<ICaseStageRollbackAppService, CaseStageRollbackAppService>();
        services.AddScoped<IKanbanAppService, KanbanAppService>();
        services.AddScoped<ICompanyAppService, CompanyAppService>();
        services.AddScoped<ICaseCommentsAuditAppService, CaseCommentsAuditAppService>();
        services.AddScoped<ICaseAuthorizationService, CaseAuthorizationService>();
        services.AddScoped<IGuaranteeAuthorizationService, GuaranteeAuthorizationService>();
        services.AddScoped<ILoanAuthorizationService, LoanAuthorizationService>();
        services.AddScoped<ICaseNumberGenerator, CaseNumberGenerator>();
        services.AddScoped<IGuaranteeCaseNumberGenerator, GuaranteeCaseNumberGenerator>();
        services.AddScoped<ILoanCaseNumberGenerator, LoanCaseNumberGenerator>();

        return services;
    }
}
