using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Domain.Entities.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public sealed class ProcessManager(
    IWorkflowRuntime workflowRuntime,
    IWorkflowCommandDispatcher commandDispatcher,
    IProcessReadModelProjector readModelProjector,
    ICoreDbContext dbContext,
    ILogger<ProcessManager> logger) : IProcessManager
{
    public async Task<Result<ProcessStartResult>> StartAsync(
        ProcessStartCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var start = await workflowRuntime.StartAsync(command, cancellationToken);
            if (start.IsFailure)
                return Result<ProcessStartResult>.Fail(start.Error!);

            await UpsertProcessStartAsync(command, start.Value!.WorkflowInstanceId, cancellationToken);

            return Result<ProcessStartResult>.Ok(new ProcessStartResult(
                command.Module,
                command.CaseId,
                start.Value!.WorkflowInstanceId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Process start failed for module {Module} case {CaseId}", command.Module, command.CaseId);
            return Result<ProcessStartResult>.Fail(Error.Unexpected(ApiMessages.UnexpectedError));
        }
    }

    public async Task<Result<ProcessCommandResult>> DispatchAsync(
        ProcessCommand command,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.DispatchAsync(command, cancellationToken);
        await RecordProcessCommandAsync(command, result.IsFailure, cancellationToken);
        return result;
    }

    public Task<Result<ProcessSnapshot>> GetSnapshotAsync(
        Core.Domain.Enums.CaseModuleType module,
        Guid caseId,
        string? actorRole,
        CancellationToken cancellationToken)
        => readModelProjector.GetSnapshotAsync(module, caseId, actorRole, cancellationToken);

    private async Task UpsertProcessStartAsync(
        ProcessStartCommand command,
        string workflowInstanceId,
        CancellationToken cancellationToken)
    {
        var process = await dbContext.ProcessInstances
            .FirstOrDefaultAsync(x => x.Module == command.Module && x.CaseId == command.CaseId, cancellationToken);

        if (process is null)
        {
            process = new ProcessInstance(
                command.Module,
                command.CaseId,
                workflowInstanceId,
                command.RequestedDefinitionId);

            await dbContext.ProcessInstances.AddAsync(process, cancellationToken);
        }
        else
        {
            process.MarkStarted(workflowInstanceId, command.RequestedDefinitionId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordProcessCommandAsync(
        ProcessCommand command,
        bool failed,
        CancellationToken cancellationToken)
    {
        var process = await dbContext.ProcessInstances
            .FirstOrDefaultAsync(x => x.Module == command.Module && x.CaseId == command.CaseId, cancellationToken);

        if (process is null)
        {
            logger.LogWarning(
                "Process command {CommandName} for module {Module} case {CaseId} has no persisted process instance.",
                command.CommandName,
                command.Module,
                command.CaseId);
            return;
        }

        if (failed)
            process.MarkFaulted(command.CommandName);
        else
            process.RecordCommand(command.CommandName, command.CorrelationId);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
