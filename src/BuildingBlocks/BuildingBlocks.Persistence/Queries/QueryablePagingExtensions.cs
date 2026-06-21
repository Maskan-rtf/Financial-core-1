using BuildingBlocks.Application.Results;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence.Queries;

public static class QueryablePagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, skip, take, total);
    }
}
