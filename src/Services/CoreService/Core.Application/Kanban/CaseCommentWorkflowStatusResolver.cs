namespace Core.Application.Kanban;

public static class CaseCommentWorkflowStatusResolver
{
    public static int? ResolveFromHistory<TStatus>(
        int? storedStatus,
        DateTimeOffset createdAt,
        string message,
        bool isRevisionRequest,
        IReadOnlyList<WorkflowHistorySnapshot<TStatus>>? history)
        where TStatus : struct, Enum
    {
        if (storedStatus.HasValue)
            return storedStatus;

        if (history is null || history.Count == 0)
            return null;

        if (isRevisionRequest)
        {
            var revision = history.FirstOrDefault(h =>
                !string.IsNullOrEmpty(h.Comment) &&
                h.Comment.Contains(message, StringComparison.Ordinal) &&
                Math.Abs((h.CreatedAt - createdAt).TotalSeconds) < 10);

            if (revision is not null)
                return Convert.ToInt32(revision.FromStatus);
        }

        var sorted = history.OrderBy(h => h.CreatedAt).ToList();
        var status = Convert.ToInt32(sorted[0].FromStatus);

        foreach (var entry in sorted)
        {
            if (entry.CreatedAt <= createdAt)
                status = Convert.ToInt32(entry.ToStatus);
            else
                break;
        }

        var simultaneous = sorted.FirstOrDefault(h =>
            Math.Abs((h.CreatedAt - createdAt).TotalSeconds) < 2);

        if (simultaneous is not null && isRevisionRequest)
            return Convert.ToInt32(simultaneous.FromStatus);

        return status;
    }
}

public sealed record WorkflowHistorySnapshot<TStatus>(
    DateTimeOffset CreatedAt,
    TStatus FromStatus,
    TStatus ToStatus,
    string? Comment)
    where TStatus : struct, Enum;
