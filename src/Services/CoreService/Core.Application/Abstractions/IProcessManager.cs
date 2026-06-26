using BuildingBlocks.Application.Results;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IProcessManager
{
    Task<Result<ProcessStartResult>> StartAsync(ProcessStartCommand command, CancellationToken cancellationToken);

    Task<Result<ProcessCommandResult>> DispatchAsync(ProcessCommand command, CancellationToken cancellationToken);

    Task<Result<ProcessSnapshot>> GetSnapshotAsync(
        CaseModuleType module,
        Guid caseId,
        string? actorRole,
        CancellationToken cancellationToken);
}

public sealed record ProcessStartCommand(
    CaseModuleType Module,
    Guid CaseId,
    string? RequestedDefinitionId = null);

public sealed record ProcessStartResult(
    CaseModuleType Module,
    Guid CaseId,
    string WorkflowInstanceId);

public sealed record ProcessCommand(
    CaseModuleType Module,
    Guid CaseId,
    string CommandName,
    string? ActorId = null,
    string? ActorRole = null,
    Guid? CorrelationId = null,
    string? Comment = null,
    string? Signal = null,
    object? Payload = null);

public sealed record ProcessCommandResult(
    CaseModuleType Module,
    Guid CaseId,
    string CommandName);
