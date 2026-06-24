using Core.Application.Abstractions;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure.Persistence;

public sealed class GuaranteeRenewalCaseRepository(CoreDbContext dbContext) : IGuaranteeRenewalCaseRepository
{
    public Task<GuaranteeRenewalCase?> GetAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.GuaranteeRenewalCases
            .Include(x => x.ParentGuaranteeCase)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<GuaranteeRenewalCase?> GetScopedAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken)
    {
        var query = dbContext.GuaranteeRenewalCases
            .Include(x => x.ParentGuaranteeCase)
            .AsQueryable();

        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task AddAsync(GuaranteeRenewalCase renewalCase, CancellationToken cancellationToken)
        => dbContext.GuaranteeRenewalCases.AddAsync(renewalCase, cancellationToken).AsTask();

    public Task<bool> ExistsCaseNumberAsync(string caseNumber, CancellationToken cancellationToken)
        => dbContext.GuaranteeRenewalCases.AnyAsync(x => x.CaseNumber == caseNumber, cancellationToken);

    public Task<string?> GetWorkflowInstanceIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.GuaranteeRenewalCases
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.WorkflowInstanceId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<GuaranteeRenewalKanbanProjection>> ListActiveKanbanProjectionsAsync(
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        var terminal = new[]
        {
            GuaranteeRenewalStatus.Completed,
            GuaranteeRenewalStatus.Rejected,
            GuaranteeRenewalStatus.Cancelled
        };

        var query = dbContext.GuaranteeRenewalCases
            .AsNoTracking()
            .Where(x => !terminal.Contains(x.CurrentStatus));

        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new GuaranteeRenewalKanbanProjection(
                x.Id,
                x.ParentGuaranteeCaseId,
                x.CaseNumber,
                x.CurrentStatus,
                x.CreatedAt,
                x.UpdatedAt,
                x.ParentGuaranteeCase.CaseNumber))
            .ToListAsync(cancellationToken);
    }
}
