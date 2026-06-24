using Core.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IGuaranteeRenewalCaseRepository
{
    Task<GuaranteeRenewalCase?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<GuaranteeRenewalCase?> GetScopedAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken);
    Task AddAsync(GuaranteeRenewalCase renewalCase, CancellationToken cancellationToken);
    Task<bool> ExistsCaseNumberAsync(string caseNumber, CancellationToken cancellationToken);
    Task<string?> GetWorkflowInstanceIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<GuaranteeRenewalKanbanProjection>> ListActiveKanbanProjectionsAsync(
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);
}

public sealed record GuaranteeRenewalKanbanProjection(
    Guid Id,
    Guid ParentGuaranteeCaseId,
    string CaseNumber,
    GuaranteeRenewalStatus CurrentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string ParentCaseNumber);
