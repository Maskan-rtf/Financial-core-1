using Core.Application.Abstractions;
using Core.Workflow.Activities;
using Core.Workflow.Common;
using Core.Workflow.Workflows;
using Elsa.Common.Models;
using Elsa.Workflows.Helpers;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Filters;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Contracts;
using Elsa.Workflows.Runtime.Filters;
using Elsa.Workflows.Runtime.Options;
using Elsa.Workflows.Runtime.Requests;
using Microsoft.Extensions.Logging;

namespace Core.Workflow.Orchestration;

public sealed class ElsaGuaranteeWorkflowOrchestrator(
    IGuaranteeCaseRepository guaranteeCaseRepository,
    IWorkflowDefinitionService workflowDefinitionService,
    IWorkflowDispatcher workflowDispatcher,
    IWorkflowResumer workflowResumer,
    IBookmarkStore bookmarkStore,
    IWorkflowInstanceManager workflowInstanceManager,
    ILogger<ElsaGuaranteeWorkflowOrchestrator> logger) : IGuaranteeWorkflowOrchestrator
{
    private const int ResumePollAttempts = 40;
    private static readonly TimeSpan ResumePollDelay = TimeSpan.FromMilliseconds(150);

    public Task<string> StartGuaranteeCaseAsync(Guid caseId, CancellationToken ct)
        => StartAsync(GuaranteeCaseWorkflow.DefinitionId, caseId, ct);

    public async Task SignalGuaranteeCaseAsync(Guid caseId, string signal, object? payload, CancellationToken ct)
    {
        var instanceId = await guaranteeCaseRepository.GetWorkflowInstanceIdAsync(caseId, ct);
        await SignalAsync(caseId, instanceId, GuaranteeCaseWorkflow.DefinitionId, signal, payload, ct);
    }

    private async Task SignalAsync(
        Guid caseId,
        string? instanceId,
        string definitionId,
        string signal,
        object? payload,
        CancellationToken ct)
    {
        _ = payload;

        if (string.IsNullOrEmpty(instanceId))
            throw new InvalidOperationException(WorkflowMessages.WorkflowInstanceMissing);

        var stimulus = new CaseSignalStimulus { CaseId = caseId, Signal = signal };

        if (await TryResumeWithStimulusAsync(stimulus, instanceId, ct))
            return;

        logger.LogWarning(
            "Elsa bookmark not matched for guarantee case {CaseId} instance {InstanceId}; resetting workflow instance and bookmarks.",
            caseId,
            instanceId);

        await ResetWorkflowAsync(caseId, definitionId, ct);

        if (await PollResumeWithStimulusAsync(stimulus, instanceId, ct))
            return;

        if (await TryResumeAnyWaitBookmarkAsync(instanceId, ct))
            return;

        logger.LogWarning(
            "Elsa workflow could not be resumed for guarantee case {CaseId} after reset; domain state was still updated.",
            caseId);
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

    private async Task<bool> TryResumeWithStimulusAsync(
        CaseSignalStimulus stimulus,
        string instanceId,
        CancellationToken ct)
    {
        var responses = await workflowResumer.ResumeAsync<WaitForCaseSignalActivity>(
            stimulus,
            instanceId,
            options: null,
            ct);

        return responses.Any();
    }

    private async Task<bool> PollResumeWithStimulusAsync(
        CaseSignalStimulus stimulus,
        string instanceId,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < ResumePollAttempts; attempt++)
        {
            if (await TryResumeWithStimulusAsync(stimulus, instanceId, ct))
                return true;

            await Task.Delay(ResumePollDelay, ct);
        }

        return false;
    }

    private async Task<bool> TryResumeAnyWaitBookmarkAsync(string instanceId, CancellationToken ct)
    {
        var activityTypeName = ActivityTypeNameHelper.GenerateTypeName<WaitForCaseSignalActivity>();
        var bookmarks = (await bookmarkStore.FindManyAsync(
            new BookmarkFilter { WorkflowInstanceId = instanceId, Name = activityTypeName },
            ct)).ToList();

        if (bookmarks.Count == 0)
            return false;

        var bookmark = bookmarks.OrderByDescending(b => b.CreatedAt).First();
        var responses = await workflowResumer.ResumeAsync(
            new BookmarkFilter { BookmarkId = bookmark.Id },
            options: null,
            ct);

        return responses.Any();
    }

    private async Task ResetWorkflowAsync(Guid caseId, string definitionId, CancellationToken ct)
    {
        var instanceId = caseId.ToString("D");

        await bookmarkStore.DeleteAsync(new BookmarkFilter { WorkflowInstanceId = instanceId }, ct);
        await workflowInstanceManager.DeleteAsync(new WorkflowInstanceFilter { Id = instanceId }, ct);

        await StartAsync(definitionId, caseId, ct);
    }
}
