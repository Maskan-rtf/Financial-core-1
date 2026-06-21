using Core.Domain.Enums;

namespace Core.Application.Requests;

public sealed record CreateGuaranteeCaseRequest(ApplicantType ApplicantType, Guid? CompanyId, string? Title = null);

public sealed record UpdateGuaranteeApplicationRequest(
    GuaranteeType? GuaranteeType,
    string? ContractSubject,
    bool? IsKnowledgeBasedProduct,
    string? BeneficiaryName,
    string? BeneficiaryNationalId,
    BeneficiaryCompanyType? BeneficiaryCompanyType,
    ApplicantCategory ApplicantCategory,
    string? ApplicantCategoryOther,
    ApplicantLegalForm? ApplicantLegalForm,
    string? BaseContractNumber,
    decimal? BaseContractAmount,
    string? BaseContractAmountInWords,
    decimal? PriceAdjustmentRatePercent,
    string? ExecutionProvince,
    decimal? RequestedGuaranteeAmount,
    int? InitialValidityDays,
    DateOnly? ValidityFrom,
    DateOnly? ValidityTo,
    string? CollateralDescription,
    string? FacilitySubject);

public sealed record UpdateGuaranteeApprovalFormRequest(
    decimal? CreditLimitWithCheck,
    decimal? FundIssuedGuaranteesTotal,
    decimal? ActiveCommitments,
    decimal? RemainingCredit,
    GuaranteeType? GuaranteeType,
    decimal? GuaranteeAmount,
    string? GuaranteeAmountInWords,
    string? ContractSubject,
    string? Beneficiary,
    DateOnly? IssuanceDate,
    DateOnly? ExpiryDate,
    int? ActiveDurationDays,
    decimal? DepositRatePercent,
    decimal? DepositAmount,
    decimal? AnnualCommissionRatePercent,
    decimal? CommissionAmount,
    string? CollateralDescription,
    string? GuarantorsDescription,
    string? OtherNotes);

public sealed record PresignGuaranteeUploadRequest(
    GuaranteeDocumentType DocumentType,
    string FileName,
    string MimeType,
    long FileSize);

public sealed record GuaranteeCaseSearchRequest(
    string? CaseNumber,
    string? ApplicantUserId,
    GuaranteeCasePhase? Phase,
    GuaranteeCaseStatus? Status,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Skip = 0,
    int Take = 20);

public sealed record CreateGuaranteeRenewalRequest(
    Guid ParentGuaranteeCaseId,
    RenewalKind RenewalKind,
    DateOnly? RequestedExpiryDate,
    decimal? RequestedAmount);

public sealed record UpdateGuaranteeRenewalDatesRequest(DateOnly ApprovedExpiryDate);

public sealed record SetGuaranteeApplicantCreditLimitRequest(
    decimal CreditLimitWithCheck,
    DateOnly PeriodStart,
    DateOnly ExpiresAt);

public sealed record SetGuaranteeFundCreditLimitRequest(
    decimal CreditLimitWithCheck,
    DateOnly PeriodStart,
    DateOnly ExpiresAt);
