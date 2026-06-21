namespace BuildingBlocks.Application.Results;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Skip,
    int Take,
    long TotalCount)
{
    public int Page => Take == 0 ? 1 : (Skip / Take) + 1;

    public int PageNumber => Page;

    public int PageSize => Take;

    public int TotalPages => Take == 0
        ? 0
        : (int)Math.Ceiling((double)TotalCount / Take);
}

