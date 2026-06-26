using BuildingBlocks.Application.Results;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IProcessReadModelProjector
{
    Task<Result<ProcessSnapshot>> GetSnapshotAsync(
        CaseModuleType module,
        Guid caseId,
        string? actorRole,
        CancellationToken cancellationToken);
}

public sealed record ProcessSnapshot(
    CaseModuleType Module,
    Guid CaseId,
    string? WorkflowInstanceId,
    int? CurrentStatus,
    int? CurrentPhase,
    IReadOnlyCollection<string> AllowedActions);
