namespace Core.Application.Common;

public static class CaseNumberFormat
{
    public const int MaxDailySequenceAttempts = 5;

    public static string Build(string prefix, DateTimeOffset utcNow, int dailySequence = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        if (dailySequence < 1)
            throw new ArgumentOutOfRangeException(nameof(dailySequence));

        var date = utcNow.ToString("yyyyMMdd");
        var baseNumber = $"{prefix}-{date}";
        return dailySequence <= 1 ? baseNumber : $"{baseNumber}-{dailySequence}";
    }
}
