namespace BuildingBlocks.Application.Requests;

public sealed record PageRequest(int Skip = 0, int Take = 20)
{
    public int NormalizedSkip => Skip < 0 ? 0 : Skip;
    public int NormalizedTake => Take is < 1 ? 20 : Take > 200 ? 200 : Take;
}

