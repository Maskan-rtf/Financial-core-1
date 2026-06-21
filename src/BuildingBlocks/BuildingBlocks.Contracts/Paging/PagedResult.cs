namespace BuildingBlocks.Contracts.Paging;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Skip,
    int Take,
    long TotalCount)
{
    public int Page => Take == 0 ? 1 : (Skip / Take) + 1;
    public int PageSize => Take;
};

