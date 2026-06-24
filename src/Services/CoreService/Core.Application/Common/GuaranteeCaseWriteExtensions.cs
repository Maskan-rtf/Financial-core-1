using Core.Domain.Entities;
using Core.Domain.Entities.Guarantee;
using Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Core.Application.Common;

public static class GuaranteeCaseWriteExtensions
{
    public static Task<int> TouchUpdatedAtAsync(
        this DbSet<GuaranteeCase> cases,
        Guid caseId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
        => cases
            .Where(c => c.Id == caseId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(c => c.UpdatedAt, updatedAt),
                cancellationToken);

    public static Task<int> SetTitleAsync(
        this DbSet<GuaranteeCase> cases,
        Guid caseId,
        string? title,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
        => cases
            .Where(c => c.Id == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.Title, title)
                    .SetProperty(c => c.UpdatedAt, updatedAt),
                cancellationToken);

    public static Task<int> ApplyStateAsync(
        this DbSet<GuaranteeCase> cases,
        Guid caseId,
        GuaranteeCaseStatus status,
        GuaranteeCasePhase phase,
        DateTimeOffset updatedAt,
        DateTimeOffset? completedAt,
        CancellationToken cancellationToken = default)
        => cases
            .Where(c => c.Id == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.CurrentStatus, status)
                    .SetProperty(c => c.CurrentPhase, phase)
                    .SetProperty(c => c.UpdatedAt, updatedAt)
                    .SetProperty(c => c.CompletedAt, completedAt),
                cancellationToken);

    public static Task<int> ApplyStateAndAmendmentAsync(
        this DbSet<GuaranteeCase> cases,
        Guid caseId,
        GuaranteeCaseStatus status,
        GuaranteeCasePhase phase,
        DateTimeOffset updatedAt,
        DateTimeOffset? completedAt,
        AmendmentType? amendmentType,
        string? amendmentReason,
        bool amendmentRequiresCreditReview,
        string? amendmentOriginalGuaranteeReference,
        bool legalOverrideApproved,
        bool settlementConfirmationRequired,
        DateOnly? amendmentRequestedValidityTo,
        decimal? amendmentRequestedAmount,
        DateOnly? amendmentApprovedValidityTo,
        decimal? amendmentApprovedAmount,
        DateTimeOffset? amendmentCreatedAt,
        DateTimeOffset? amendmentCompletedAt,
        CancellationToken cancellationToken = default)
        => cases
            .Where(c => c.Id == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.CurrentStatus, status)
                    .SetProperty(c => c.CurrentPhase, phase)
                    .SetProperty(c => c.UpdatedAt, updatedAt)
                    .SetProperty(c => c.CompletedAt, completedAt)
                    .SetProperty(c => c.AmendmentType, amendmentType)
                    .SetProperty(c => c.AmendmentReason, amendmentReason)
                    .SetProperty(c => c.AmendmentRequiresCreditReview, amendmentRequiresCreditReview)
                    .SetProperty(c => c.AmendmentOriginalGuaranteeReference, amendmentOriginalGuaranteeReference)
                    .SetProperty(c => c.LegalOverrideApproved, legalOverrideApproved)
                    .SetProperty(c => c.SettlementConfirmationRequired, settlementConfirmationRequired)
                    .SetProperty(c => c.AmendmentRequestedValidityTo, amendmentRequestedValidityTo)
                    .SetProperty(c => c.AmendmentRequestedAmount, amendmentRequestedAmount)
                    .SetProperty(c => c.AmendmentApprovedValidityTo, amendmentApprovedValidityTo)
                    .SetProperty(c => c.AmendmentApprovedAmount, amendmentApprovedAmount)
                    .SetProperty(c => c.AmendmentCreatedAt, amendmentCreatedAt)
                    .SetProperty(c => c.AmendmentCompletedAt, amendmentCompletedAt),
                cancellationToken);

    public static async Task<int> ApplyLatestPendingAmendmentAuditDecisionAsync(
        this DbSet<GuaranteeAmendmentHistoryRecord> records,
        Guid caseId,
        GuaranteeAmendmentHistoryStatus status,
        string approvalUser,
        DateTimeOffset decidedAt,
        string? decisionReason,
        CancellationToken cancellationToken = default)
    {
        var auditId = await records
            .AsNoTracking()
            .Where(x => x.GuaranteeCaseId == caseId && x.Status == GuaranteeAmendmentHistoryStatus.PendingReview)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (auditId == Guid.Empty)
            return 0;

        return await records
            .Where(x => x.Id == auditId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.ApprovalUser, approvalUser)
                    .SetProperty(x => x.ApprovedAt, decidedAt)
                    .SetProperty(x => x.DecisionReason, decisionReason)
                    .SetProperty(x => x.UpdatedAt, decidedAt),
                cancellationToken);
    }

    public static Task InsertWorkflowHistoryAsync(
        this DbContext db,
        GuaranteeCaseWorkflowHistory history,
        CancellationToken cancellationToken = default)
        => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Guarantee"."guarantee_case_workflow_history"
                ("Id", "CaseId", "FromPhase", "ToPhase", "FromStatus", "ToStatus", "ChangedByUserId", "Action", "ActorRole", "CorrelationId", "Comment", "CreatedAt")
            VALUES
                ({history.Id}, {history.CaseId}, {(int)history.FromPhase}, {(int)history.ToPhase}, {(int)history.FromStatus}, {(int)history.ToStatus}, {history.ChangedByUserId}, {history.Action}, {history.ActorRole}, {history.CorrelationId}, {history.Comment}, {history.CreatedAt})
            """, cancellationToken);

    public static Task InsertCommentAsync(
        this DbContext db,
        GuaranteeCaseComment comment,
        CancellationToken cancellationToken = default)
        => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Guarantee"."guarantee_case_comments"
                ("Id", "CaseId", "Phase", "SenderUserId", "SenderRole", "Message", "IsRevisionRequest", "IsInternal", "ParentId", "CreatedAt")
            VALUES
                ({comment.Id}, {comment.CaseId}, {(int)comment.Phase}, {comment.SenderUserId}, {comment.SenderRole}, {comment.Message}, {comment.IsRevisionRequest}, {comment.IsInternal}, {comment.ParentId}, {comment.CreatedAt})
            """, cancellationToken);

    public static Task InsertAmendmentHistoryRecordAsync(
        this DbContext db,
        GuaranteeAmendmentHistoryRecord record,
        CancellationToken cancellationToken = default)
        => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Guarantee"."guarantee_amendment_history_records"
                ("Id", "GuaranteeCaseId", "AmendmentType", "PreviousValues", "NewValues", "Reason", "CreatedBy", "CreatedAt", "Status")
            VALUES
                ({record.Id}, {record.GuaranteeCaseId}, {(int)record.AmendmentType}, CAST({record.PreviousValues} AS jsonb), CAST({record.NewValues} AS jsonb), {record.Reason}, {record.CreatedBy}, {record.CreatedAt}, {(int)record.Status})
            """, cancellationToken);
}
