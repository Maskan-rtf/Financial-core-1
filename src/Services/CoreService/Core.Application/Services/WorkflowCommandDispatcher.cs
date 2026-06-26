using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Domain.Constants;

namespace Core.Application.Services;

public sealed class WorkflowCommandDispatcher(IWorkflowRuntime workflowRuntime) : IWorkflowCommandDispatcher
{
    public async Task<Result<ProcessCommandResult>> DispatchAsync(
        ProcessCommand command,
        CancellationToken cancellationToken)
    {
        var signal = string.IsNullOrWhiteSpace(command.Signal)
            ? WorkflowSignals.StatusChanged
            : command.Signal;

        var signalResult = await workflowRuntime.SignalAsync(
            new ProcessSignalCommand(command, signal, command.Payload),
            cancellationToken);

        return signalResult.IsFailure
            ? Result<ProcessCommandResult>.Fail(signalResult.Error!)
            : Result<ProcessCommandResult>.Ok(new ProcessCommandResult(
                command.Module,
                command.CaseId,
                command.CommandName));
    }
}

