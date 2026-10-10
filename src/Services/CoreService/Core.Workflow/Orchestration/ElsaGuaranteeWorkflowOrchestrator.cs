using Core.Application.Abstractions;
using Core.Workflow.Common;
using Core.Workflow.Workflows;
using Elsa.Common.Models;
using Elsa.Workflows.Management;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime.Messages;
using IElsaWorkflowRuntime = Elsa.Workflows.Runtime.IWorkflowRuntime;
using Microsoft.Extensions.Logging;

namespace Core.Workflow.Orchestration;

public sealed class ElsaGuaranteeWorkflowOrchestrator(
    IWorkflowDefinitionService workflowDefinitionService,
    IElsaWorkflowRuntime workflowRuntime,
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
        var client = await workflowRuntime.CreateClientAsync(instanceId, ct);
        await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
        {
            WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionVersionId(definition.Id),
            CorrelationId = instanceId,
            Input = new Dictionary<string, object> { ["CaseId"] = caseId }
        }, ct);
        return instanceId;
    }
}
