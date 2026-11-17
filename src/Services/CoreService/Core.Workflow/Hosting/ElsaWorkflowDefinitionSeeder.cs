using Core.Workflow.Workflows;
using Elsa.Common.Models;
using Elsa.Workflows;
using Elsa.Workflows.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Workflow.Hosting;

/// <summary>
/// In production Elsa persists definitions in PostgreSQL; code-first workflows must be published once.
/// </summary>
public sealed class ElsaWorkflowDefinitionSeeder(
    IHostApplicationLifetime lifetime,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<ElsaWorkflowDefinitionSeeder> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment())
            return Task.CompletedTask;

        lifetime.ApplicationStarted.Register(() => _ = SeedDefinitionsAsync());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedDefinitionsAsync()
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var definitionService = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionService>();
            var publisher = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionPublisher>();
            var builderFactory = scope.ServiceProvider.GetRequiredService<IWorkflowBuilderFactory>();

            await EnsurePublishedAsync<InvestmentCaseWorkflow>(
                definitionService, publisher, builderFactory, InvestmentCaseWorkflow.DefinitionId);
            await EnsurePublishedAsync<GuaranteeCaseWorkflow>(
                definitionService, publisher, builderFactory, GuaranteeCaseWorkflow.DefinitionId);
            await EnsurePublishedAsync<LoanCaseWorkflow>(
                definitionService, publisher, builderFactory, LoanCaseWorkflow.DefinitionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish Elsa workflow definitions on startup");
        }
    }

    private async Task EnsurePublishedAsync<TWorkflow>(
        IWorkflowDefinitionService definitionService,
        IWorkflowDefinitionPublisher publisher,
        IWorkflowBuilderFactory builderFactory,
        string definitionId)
        where TWorkflow : WorkflowBase, ICodeFirstWorkflow, new()
    {
        var existing = await definitionService.FindWorkflowDefinitionAsync(
            definitionId,
            VersionOptions.Published,
            CancellationToken.None);

        if (existing is not null)
        {
            logger.LogDebug("Elsa workflow definition already published: {DefinitionId}", definitionId);
            return;
        }

        var builder = builderFactory.CreateBuilder();
        var template = new TWorkflow();
        template.PopulateBuilder(builder);

        if (builder.Root is null)
            throw new InvalidOperationException($"Workflow {definitionId} has no root activity after build.");

        var draft = await publisher.NewAsync(builder.Root, CancellationToken.None);
        await publisher.PublishAsync(draft, CancellationToken.None);
        logger.LogInformation("Published Elsa workflow definition {DefinitionId}", definitionId);
    }
}
