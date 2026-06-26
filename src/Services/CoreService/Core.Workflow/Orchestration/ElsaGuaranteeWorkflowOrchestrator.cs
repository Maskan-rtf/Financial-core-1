using Core.Application.Abstractions;
using Core.Workflow.Common;
using Core.Workflow.Workflows;
using Elsa.Common.Models;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Filters;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Contracts;
using Elsa.Workflows.Runtime.Requests;
using Microsoft.Extensions.Logging;

namespace Core.Workflow.Orchestration;

public sealed class ElsaGuaranteeWorkflowOrchestrator(
    IWorkflowDefinitionService workflowDefinitionService,
    IWorkflowDispatcher workflowDispatcher,
    ILogger<ElsaGuaranteeWorkflowOrchestrator> logger) : IGuaranteeWorkflowOrchestrator
{
    public Task<string> StartGuaranteeCaseAsync(Guid caseId, CancellationToken ct)
        => StartAsync(GuaranteeCaseWorkflow.DefinitionId, caseId, ct);

    public Task SignalGuaranteeCaseAsync(Guid caseId, string signal, object? payload, CancellationToken ct)
    {
        _ = signal;
        _ = payload;
        _ = ct;

        logger.LogDebug(
            "Ignoring legacy guarantee status signal for case {CaseId}; Elsa command bookmarks own workflow routing.",
            caseId);
        return Task.CompletedTask;
    }

    private async Task<string> StartAsync(string definitionId, Guid caseId, CancellationToken ct)
    {
        var definition = await workflowDefinitionService.FindWorkflowDefinitionAsync(
            definitionId,
            VersionOptions.Latest,
            ct);

        if (definition is null)
            throw new InvalidOperationException(WorkflowMessages.DefinitionNotFound);

        var instanceId = caseId.ToString("D");
        var request = new DispatchWorkflowDefinitionRequest(definition.Id)
        {
            Input = new Dictionary<string, object> { ["CaseId"] = caseId },
            InstanceId = instanceId,
            CorrelationId = instanceId
        };

        await workflowDispatcher.DispatchAsync(request, ct);
        return instanceId;
    }
}
