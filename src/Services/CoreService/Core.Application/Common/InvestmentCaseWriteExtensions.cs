using Core.Domain.Entities;
using Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Core.Application.Common;

public static class InvestmentCaseWriteExtensions
{
    public static Task<int> TouchUpdatedAtAsync(
        this DbSet<InvestmentCase> cases,
        Guid caseId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
        => cases
            .Where(c => c.Id == caseId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(c => c.UpdatedAt, updatedAt),
                cancellationToken);

    public static Task<int> SetTitleAsync(
        this DbSet<InvestmentCase> cases,
        Guid caseId,
        string? title,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
        => cases
            .Where(c => c.Id == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.Title, title)
                    .SetProperty(c => c.UpdatedAt, updatedAt),
                cancellationToken);

    public static Task<int> ApplyStateAsync(
        this DbSet<InvestmentCase> cases,
        Guid caseId,
        CaseStatus status,
        CasePhase phase,
        DateTimeOffset updatedAt,
        DateTimeOffset? completedAt,
        CancellationToken cancellationToken = default)
        => cases
            .Where(c => c.Id == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.CurrentStatus, status)
                    .SetProperty(c => c.CurrentPhase, phase)
                    .SetProperty(c => c.UpdatedAt, updatedAt)
                    .SetProperty(c => c.CompletedAt, completedAt),
                cancellationToken);
}
