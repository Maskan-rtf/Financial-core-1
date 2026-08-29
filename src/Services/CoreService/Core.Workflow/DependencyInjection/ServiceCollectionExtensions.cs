using BuildingBlocks.Application.Common;
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Core.Application.Abstractions;
using Core.Workflow.Orchestration;
using Core.Workflow.Workflows;

namespace Core.Workflow.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreWorkflow(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddElsa(elsa =>
        {
            if (!environment.IsDevelopment())
            {
                var connectionString = configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException(SystemMessages.PostgresConnectionMissing);

                elsa.UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef => ef.UsePostgreSql(connectionString)));
                elsa.UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef => ef.UsePostgreSql(connectionString)));
            }

            elsa.UseWorkflowsApi();
            elsa.AddWorkflow<InvestmentCaseWorkflow>();
            elsa.AddWorkflow<GuaranteeCaseWorkflow>();
            elsa.AddWorkflow<LoanCaseWorkflow>();
        });

        services.AddScoped<ICaseWorkflowOrchestrator, ElsaCaseWorkflowOrchestrator>();
        services.AddScoped<IGuaranteeWorkflowOrchestrator, ElsaGuaranteeWorkflowOrchestrator>();
        services.AddScoped<ILoanWorkflowOrchestrator, ElsaLoanWorkflowOrchestrator>();
        services.AddScoped<ElsaCommandBookmarkReader>();
        services.AddScoped<IWorkflowRuntime, ElsaWorkflowRuntime>();
        services.AddScoped<IInvestmentWorkflowActionProvider, InvestmentWorkflowActionProvider>();
        services.AddScoped<IGuaranteeWorkflowActionProvider, GuaranteeWorkflowActionProvider>();
        services.AddScoped<ILoanWorkflowActionProvider, LoanWorkflowActionProvider>();
        return services;
    }
}
