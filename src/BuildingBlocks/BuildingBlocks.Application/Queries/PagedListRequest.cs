namespace BuildingBlocks.Application.Queries;

/// <summary>
/// Base request for advanced list endpoints. Extend per module (Guarantee, Loan, Investment).
/// </summary>
public record PagedListRequest
{
    public int Skip { get; init; }
    public int Take { get; init; } = 20;
    public string? SortBy { get; init; }
    public SortDirection SortDirection { get; init; } = SortDirection.Desc;

    // Backward-compatible aliases for older callers that still send page-based query parameters.
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }

    public int NormalizedTake => (PageSize ?? Take) switch
    {
        < 1 => 1,
        > 200 => 200,
        var size => size
    };

    public int NormalizedSkip
    {
        get
        {
            if (PageSize.HasValue || PageNumber.HasValue)
            {
                if (PageNumber is > 1)
                    return (PageNumber.Value - 1) * NormalizedTake;

                return 0;
            }

            if (Skip >= 0)
                return Skip;

            return 0;
        }
    }
}
