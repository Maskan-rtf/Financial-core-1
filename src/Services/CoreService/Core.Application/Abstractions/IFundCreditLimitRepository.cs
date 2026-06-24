using BuildingBlocks.Application.Results;
using Core.Application.Requests;
using Core.Domain.Entities.Fund;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IFundCreditLimitRepository
{
    Task<FundCreditLimit?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<FundCreditLimitPoolProjection?> GetPoolProjectionAsync(Guid id, CancellationToken cancellationToken);

    Task<FundCreditLimitPoolProjection?> ResolveActivePoolAsync(
        FundModuleType moduleType,
        DateOnly referenceDate,
        CancellationToken cancellationToken);

    Task<bool> HasOverlappingPeriodAsync(
        FundModuleType moduleType,
        DateOnly periodStart,
        DateOnly expiresAt,
        Guid? excludeId,
        CancellationToken cancellationToken);

    Task AddAsync(FundCreditLimit row, CancellationToken cancellationToken);

    Task<FundCreditLimit?> GetAsNoTrackingAsync(Guid id, CancellationToken cancellationToken);

    Task RemoveAsync(FundCreditLimit row, CancellationToken cancellationToken);

    Task<PagedResult<FundCreditLimit>> GetPagedAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FundCreditLimit>> ListOrderedAsync(CancellationToken cancellationToken);
}
