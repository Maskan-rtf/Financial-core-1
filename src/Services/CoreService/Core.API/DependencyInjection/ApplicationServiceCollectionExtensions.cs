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

        services.AddScoped<IProcessManager, ProcessManager>();
        services.AddScoped<IWorkflowCommandDispatcher, WorkflowCommandDispatcher>();
        services.AddScoped<IProcessReadModelProjector, ProcessReadModelProjector>();
        services.AddScoped<IInvestmentWorkflowCommandExecutor, InvestmentWorkflowCommandExecutor>();
        services.AddScoped<IGuaranteeWorkflowCommandExecutor, GuaranteeWorkflowCommandExecutor>();
        services.AddScoped<ILoanWorkflowCommandExecutor, LoanWorkflowCommandExecutor>();
        services.AddScoped<IInvestmentWorkflowCoordinator, InvestmentWorkflowCoordinator>();
        services.AddScoped<IInvestmentCaseAppService, InvestmentCaseAppService>();
        services.AddScoped<IGuaranteeCaseAppService, GuaranteeCaseAppService>();
        services.AddScoped<IGuaranteeRenewalAppService, GuaranteeRenewalAppService>();
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
