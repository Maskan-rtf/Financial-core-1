using BuildingBlocks.Application.Results;

namespace Core.Application.Abstractions;

public interface IWorkflowRuntime
{
    Task<Result<WorkflowStartResult>> StartAsync(ProcessStartCommand command, CancellationToken cancellationToken);

    Task<Result> SignalAsync(ProcessSignalCommand command, CancellationToken cancellationToken);
}

public sealed record WorkflowStartResult(string WorkflowInstanceId);

public sealed record ProcessSignalCommand(
    ProcessCommand ProcessCommand,
    string Signal,
    object? Payload);

