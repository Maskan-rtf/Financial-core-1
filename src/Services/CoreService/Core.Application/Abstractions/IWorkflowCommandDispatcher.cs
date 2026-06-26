using BuildingBlocks.Application.Results;

namespace Core.Application.Abstractions;

public interface IWorkflowCommandDispatcher
{
    Task<Result<ProcessCommandResult>> DispatchAsync(ProcessCommand command, CancellationToken cancellationToken);
}

