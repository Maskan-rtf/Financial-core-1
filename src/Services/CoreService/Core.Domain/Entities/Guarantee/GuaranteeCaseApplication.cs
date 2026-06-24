using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Domain.Entities.Guarantee;

public sealed class GuaranteeCaseApplication : Entity<Guid>, IAuditableEntity
{
    private GuaranteeCaseApplication()
    {
    }

    public GuaranteeCaseApplication(Guid caseId)
    {
        Id = Guid.NewGuid();
        CaseId = caseId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CaseId { get; private set; }
    public GuaranteeCase Case { get; private set; } = default!;

    public GuaranteeType? GuaranteeType { get; private set; }
    public string? ContractSubject { get; private set; }
    public bool? IsKnowledgeBasedProduct { get; private set; }
    public string? BeneficiaryName { get; private set; }
    public string? BeneficiaryNationalId { get; private set; }
    public BeneficiaryCompanyType? BeneficiaryCompanyType { get; private set; }
    public ApplicantCategory ApplicantCategory { get; private set; }
    public string? ApplicantCategoryOther { get; private set; }
    public ApplicantLegalForm? ApplicantLegalForm { get; private set; }
    public string? BaseContractNumber { get; private set; }
    public decimal? BaseContractAmount { get; private set; }
    public string? BaseContractAmountInWords { get; private set; }
    public decimal? PriceAdjustmentRatePercent { get; private set; }
    public string? ExecutionProvince { get; private set; }
    public decimal? RequestedGuaranteeAmount { get; private set; }
    public int? InitialValidityDays { get; private set; }
    public DateOnly? ValidityFrom { get; private set; }
    public DateOnly? ValidityTo { get; private set; }
    public string? CollateralDescription { get; private set; }
    public string? FacilitySubject { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(
        GuaranteeType? guaranteeType,
        string? contractSubject,
        bool? isKnowledgeBasedProduct,
        string? beneficiaryName,
        string? beneficiaryNationalId,
        BeneficiaryCompanyType? beneficiaryCompanyType,
        ApplicantCategory applicantCategory,
        string? applicantCategoryOther,
        ApplicantLegalForm? applicantLegalForm,
        string? baseContractNumber,
        decimal? baseContractAmount,
        string? baseContractAmountInWords,
        decimal? priceAdjustmentRatePercent,
        string? executionProvince,
        decimal? requestedGuaranteeAmount,
        int? initialValidityDays,
        DateOnly? validityFrom,
        DateOnly? validityTo,
        string? collateralDescription,
        string? facilitySubject)
    {
        GuaranteeType = guaranteeType;
        ContractSubject = contractSubject;
        IsKnowledgeBasedProduct = isKnowledgeBasedProduct;
        BeneficiaryName = beneficiaryName;
        BeneficiaryNationalId = beneficiaryNationalId;
        BeneficiaryCompanyType = beneficiaryCompanyType;
        ApplicantCategory = applicantCategory;
        ApplicantCategoryOther = applicantCategoryOther;
        ApplicantLegalForm = applicantLegalForm;
        BaseContractNumber = baseContractNumber;
        BaseContractAmount = baseContractAmount;
        BaseContractAmountInWords = baseContractAmountInWords;
        PriceAdjustmentRatePercent = priceAdjustmentRatePercent;
        ExecutionProvince = executionProvince;
        RequestedGuaranteeAmount = requestedGuaranteeAmount;
        InitialValidityDays = initialValidityDays;
        ValidityFrom = validityFrom;
        ValidityTo = validityTo;
        CollateralDescription = collateralDescription;
        FacilitySubject = facilitySubject;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyAmendmentExtension(DateOnly? validityTo)
    {
        if (!validityTo.HasValue)
            return;

        ValidityTo = validityTo;

        if (ValidityFrom.HasValue)
            InitialValidityDays = Math.Max(0, validityTo.Value.DayNumber - ValidityFrom.Value.DayNumber);

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyAmendmentReduction(decimal? requestedGuaranteeAmount)
    {
        if (!requestedGuaranteeAmount.HasValue)
            return;

        RequestedGuaranteeAmount = requestedGuaranteeAmount;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
