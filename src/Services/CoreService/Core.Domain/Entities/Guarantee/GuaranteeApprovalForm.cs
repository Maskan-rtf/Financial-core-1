using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Domain.Entities.Guarantee;

public sealed class GuaranteeApprovalForm : Entity<Guid>, IAuditableEntity
{
    private GuaranteeApprovalForm()
    {
    }

    public GuaranteeApprovalForm(Guid caseId)
    {
        Id = Guid.NewGuid();
        CaseId = caseId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CaseId { get; private set; }
    public GuaranteeCase Case { get; private set; } = default!;

    public decimal? CreditLimitWithCheck { get; private set; }
    public decimal? FundIssuedGuaranteesTotal { get; private set; }
    public decimal? ActiveCommitments { get; private set; }
    public decimal? RemainingCredit { get; private set; }
    public GuaranteeType? GuaranteeType { get; private set; }
    public decimal? GuaranteeAmount { get; private set; }
    public string? GuaranteeAmountInWords { get; private set; }
    public string? ContractSubject { get; private set; }
    public string? Beneficiary { get; private set; }
    public DateOnly? IssuanceDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public int? ActiveDurationDays { get; private set; }
    public decimal? DepositRatePercent { get; private set; }
    public decimal? DepositAmount { get; private set; }
    public decimal? AnnualCommissionRatePercent { get; private set; }
    public decimal? CommissionAmount { get; private set; }
    public string? CollateralDescription { get; private set; }
    public string? GuarantorsDescription { get; private set; }
    public string? OtherNotes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(
        decimal? creditLimitWithCheck,
        decimal? fundIssuedGuaranteesTotal,
        decimal? activeCommitments,
        decimal? remainingCredit,
        GuaranteeType? guaranteeType,
        decimal? guaranteeAmount,
        string? guaranteeAmountInWords,
        string? contractSubject,
        string? beneficiary,
        DateOnly? issuanceDate,
        DateOnly? expiryDate,
        int? activeDurationDays,
        decimal? depositRatePercent,
        decimal? depositAmount,
        decimal? annualCommissionRatePercent,
        decimal? commissionAmount,
        string? collateralDescription,
        string? guarantorsDescription,
        string? otherNotes)
    {
        CreditLimitWithCheck = creditLimitWithCheck;
        FundIssuedGuaranteesTotal = fundIssuedGuaranteesTotal;
        ActiveCommitments = activeCommitments;
        RemainingCredit = remainingCredit;
        GuaranteeType = guaranteeType;
        GuaranteeAmount = guaranteeAmount;
        GuaranteeAmountInWords = guaranteeAmountInWords;
        ContractSubject = contractSubject;
        Beneficiary = beneficiary;
        IssuanceDate = issuanceDate;
        ExpiryDate = expiryDate;
        ActiveDurationDays = activeDurationDays;
        DepositRatePercent = depositRatePercent;
        DepositAmount = depositAmount;
        AnnualCommissionRatePercent = annualCommissionRatePercent;
        CommissionAmount = commissionAmount;
        CollateralDescription = collateralDescription;
        GuarantorsDescription = guarantorsDescription;
        OtherNotes = otherNotes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyAmendmentExtension(DateOnly? expiryDate)
    {
        if (!expiryDate.HasValue)
            return;

        ExpiryDate = expiryDate;

        if (IssuanceDate.HasValue)
            ActiveDurationDays = Math.Max(0, expiryDate.Value.DayNumber - IssuanceDate.Value.DayNumber);

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyAmendmentReduction(decimal? guaranteeAmount)
    {
        if (!guaranteeAmount.HasValue)
            return;

        GuaranteeAmount = guaranteeAmount;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
