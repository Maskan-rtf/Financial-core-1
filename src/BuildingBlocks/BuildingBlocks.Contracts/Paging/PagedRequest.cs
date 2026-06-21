namespace BuildingBlocks.Contracts.Paging;

public sealed record PagedRequest(int Skip = 0, int Take = 25, string? Sort = null)
{
    public int Skip { get; init; } = Skip < 0 ? 0 : Skip;
    public int Take { get; init; } = Take switch
    {
        < 1 => 1,
        > 200 => 200,
        _ => Take
    };

    // Backward-compatible aliases for legacy page-based callers.
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public int NormalizedTake => PageSize switch
    {
        null => Take,
        < 1 => 1,
        > 200 => 200,
        var size => size.Value
    };

    public int NormalizedSkip
    {
        get
        {
            if (PageSize.HasValue || Page.HasValue)
            {
                if (Page is > 1)
                    return (Page.Value - 1) * NormalizedTake;

                return 0;
            }

            return Skip < 0 ? 0 : Skip;
        }
    }
}

