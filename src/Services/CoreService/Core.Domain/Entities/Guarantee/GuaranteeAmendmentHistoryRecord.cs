using BuildingBlocks.Domain.Abstractions;

namespace Core.Domain.Entities.Guarantee;

public sealed class GuaranteeAmendmentHistoryRecord : IAuditableEntity
{
    private GuaranteeAmendmentHistoryRecord()
    {
        PreviousValues = default!;
        NewValues = default!;
        Reason = default!;
        CreatedBy = default!;
    }

    public GuaranteeAmendmentHistoryRecord(
        Guid guaranteeCaseId,
        Enums.AmendmentType amendmentType,
        string previousValues,
        string newValues,
        string reason,
        string createdBy,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        GuaranteeCaseId = guaranteeCaseId;
        AmendmentType = amendmentType;
        PreviousValues = previousValues;
        NewValues = newValues;
        Reason = reason;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Status = Enums.GuaranteeAmendmentHistoryStatus.PendingReview;
    }

    public Guid Id { get; private set; }
    public Guid GuaranteeCaseId { get; private set; }
    public GuaranteeCase GuaranteeCase { get; private set; } = default!;
    public Enums.AmendmentType AmendmentType { get; private set; }
    public string PreviousValues { get; private set; }
    public string NewValues { get; private set; }
    public string Reason { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ApprovalUser { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Enums.GuaranteeAmendmentHistoryStatus Status { get; private set; }
    public string? DecisionReason { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void MarkApproved(string approvalUser, DateTimeOffset approvedAt, string? decisionReason)
    {
        ApprovalUser = approvalUser;
        ApprovedAt = approvedAt;
        Status = Enums.GuaranteeAmendmentHistoryStatus.Approved;
        DecisionReason = string.IsNullOrWhiteSpace(decisionReason) ? null : decisionReason.Trim();
        UpdatedAt = approvedAt;
    }

    public void MarkRejected(string approvalUser, DateTimeOffset approvedAt, string? decisionReason)
    {
        ApprovalUser = approvalUser;
        ApprovedAt = approvedAt;
        Status = Enums.GuaranteeAmendmentHistoryStatus.Rejected;
        DecisionReason = string.IsNullOrWhiteSpace(decisionReason) ? null : decisionReason.Trim();
        UpdatedAt = approvedAt;
    }
}
