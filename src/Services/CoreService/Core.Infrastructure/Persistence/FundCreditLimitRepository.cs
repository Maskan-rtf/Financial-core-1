using BuildingBlocks.Application.Results;
using BuildingBlocks.Persistence.Queries;
using Core.Application.Abstractions;
using Core.Application.Requests;
using Core.Domain.Entities.Fund;
using Core.Domain.Enums;
using Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure.Persistence;

public sealed class FundCreditLimitRepository(CoreDbContext dbContext) : IFundCreditLimitRepository
{
    public Task<FundCreditLimit?> GetAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.FundCreditLimits.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<FundCreditLimitPoolProjection?> GetPoolProjectionAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.FundCreditLimits
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new FundCreditLimitPoolProjection(
                x.Id,
                x.ModuleType,
                x.CreditLimitWithCheck,
                x.PeriodStart,
                x.ExpiresAt))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<FundCreditLimitPoolProjection?> ResolveActivePoolAsync(
        FundModuleType moduleType,
        DateOnly referenceDate,
        CancellationToken cancellationToken)
        => dbContext.FundCreditLimits
            .AsNoTracking()
            .Where(x => x.ModuleType == moduleType
                && x.PeriodStart <= referenceDate
                && x.ExpiresAt >= referenceDate)
            .OrderByDescending(x => x.PeriodStart)
            .Select(x => new FundCreditLimitPoolProjection(
                x.Id,
                x.ModuleType,
                x.CreditLimitWithCheck,
                x.PeriodStart,
                x.ExpiresAt))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOverlappingPeriodAsync(
        FundModuleType moduleType,
        DateOnly periodStart,
        DateOnly expiresAt,
        Guid? excludeId,
        CancellationToken cancellationToken)
        => dbContext.FundCreditLimits
            .AsNoTracking()
            .AnyAsync(
                x => x.ModuleType == moduleType
                    && x.Id != excludeId
                    && periodStart <= x.ExpiresAt
                    && expiresAt >= x.PeriodStart,
                cancellationToken);

    public Task AddAsync(FundCreditLimit row, CancellationToken cancellationToken)
        => dbContext.FundCreditLimits.AddAsync(row, cancellationToken).AsTask();

    public Task<FundCreditLimit?> GetAsNoTrackingAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.FundCreditLimits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task RemoveAsync(FundCreditLimit row, CancellationToken cancellationToken)
    {
        dbContext.FundCreditLimits.Remove(row);
        return Task.CompletedTask;
    }

    public Task<PagedResult<FundCreditLimit>> GetPagedAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
        => dbContext.FundCreditLimits
            .AsNoTracking()
            .OrderByDescending(x => x.ModuleType)
            .ThenByDescending(x => x.PeriodStart)
            .ToPagedResultAsync(skip, take, cancellationToken);

    public async Task<IReadOnlyList<FundCreditLimit>> ListOrderedAsync(CancellationToken cancellationToken)
        => await dbContext.FundCreditLimits
            .AsNoTracking()
            .OrderByDescending(x => x.ModuleType)
            .ThenByDescending(x => x.PeriodStart)
            .ToListAsync(cancellationToken);
}
