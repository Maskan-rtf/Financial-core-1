using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Domain.Entities;

namespace Core.Domain.Entities.Loan;

public sealed class LoanDisbursementPlanItem : Entity<Guid>, IAuditableEntity
{
    private LoanDisbursementPlanItem()
    {
    }

    public LoanDisbursementPlanItem(
        Guid caseId,
        int stageNumber,
        decimal amount,
        DateOnly? plannedDate,
        string? notes)
    {
        Id = Guid.NewGuid();
        CaseId = caseId;
        StageNumber = stageNumber;
        Amount = amount;
        PlannedDate = plannedDate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CaseId { get; private set; }
    public LoanCase Case { get; private set; } = default!;

    public int StageNumber { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly? PlannedDate { get; private set; }
    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
}
