using BuildingBlocks.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Domain.Entities.Workflow;

public sealed class ProcessInstance : Entity<Guid>
{
    private ProcessInstance()
    {
        WorkflowInstanceId = default!;
    }

    public ProcessInstance(
        CaseModuleType module,
        Guid caseId,
        string workflowInstanceId,
        string? workflowDefinitionId = null,
        int? workflowDefinitionVersion = null)
    {
        Id = Guid.NewGuid();
        Module = module;
        CaseId = caseId;
        WorkflowInstanceId = workflowInstanceId;
        WorkflowDefinitionId = Normalize(workflowDefinitionId, 128);
        WorkflowDefinitionVersion = workflowDefinitionVersion;
        Status = ProcessExecutionStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
        UpdatedAt = StartedAt;
    }

    public CaseModuleType Module { get; private set; }
    public Guid CaseId { get; private set; }
    public string WorkflowInstanceId { get; private set; }
    public string? WorkflowDefinitionId { get; private set; }
    public int? WorkflowDefinitionVersion { get; private set; }
    public ProcessExecutionStatus Status { get; private set; }
    public string? CurrentProcessStep { get; private set; }
    public Guid? LastCommandCorrelationId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void MarkStarted(
        string workflowInstanceId,
        string? workflowDefinitionId = null,
        int? workflowDefinitionVersion = null)
    {
        WorkflowInstanceId = workflowInstanceId;
        WorkflowDefinitionId = Normalize(workflowDefinitionId, 128);
        WorkflowDefinitionVersion = workflowDefinitionVersion;
        Status = ProcessExecutionStatus.Running;
        CompletedAt = null;
        Touch();
    }

    public void RecordCommand(string commandName, Guid? correlationId)
    {
        CurrentProcessStep = Normalize(commandName, 128);
        LastCommandCorrelationId = correlationId;
        Touch();
    }

    public void MarkCompleted()
    {
        Status = ProcessExecutionStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkFaulted(string? step = null)
    {
        Status = ProcessExecutionStatus.Faulted;
        CurrentProcessStep = Normalize(step, 128) ?? CurrentProcessStep;
        Touch();
    }

    public void MarkCancelled(string? step = null)
    {
        Status = ProcessExecutionStatus.Cancelled;
        CurrentProcessStep = Normalize(step, 128) ?? CurrentProcessStep;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
