using System.Text.Json.Serialization;
using Core.Application.Requests;
using Core.Domain.Enums;

namespace Core.Application.DTOs;

[JsonDerivedType(typeof(GuaranteeCaseApplicantDto), typeDiscriminator: "applicant")]
[JsonDerivedType(typeof(GuaranteeCaseInternalDto), typeDiscriminator: "internal")]
public abstract record GuaranteeCaseDto(
    Guid Id,
    string CaseNumber,
    string? Title,
    ApplicantType ApplicantType,
    GuaranteeCasePhase CurrentPhase,
    GuaranteeCaseStatus CurrentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record GuaranteeCaseApplicantDto(
    Guid Id,
    string CaseNumber,
    string? Title,
    ApplicantType ApplicantType,
    GuaranteeCasePhase CurrentPhase,
    GuaranteeCaseStatus CurrentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt,
    CompanyDto? Company,
    GuaranteeApplicationDto? Application = null,
    GuaranteeApprovalFormDto? ApprovalForm = null,
    GuaranteeApplicantCreditSnapshotDto? ApplicantCreditSnapshot = null,
    FundCreditCapacitySnapshotDto? FundCreditCapacity = null)
    : GuaranteeCaseDto(Id, CaseNumber, Title, ApplicantType, CurrentPhase, CurrentStatus, CreatedAt, UpdatedAt, CompletedAt);

public sealed record GuaranteeCaseInternalDto(
    Guid Id,
    string CaseNumber,
    string? Title,
    string ApplicantUserId,
    string? ApplicantFullName,
    string? ApplicantPhoneNumber,
    ApplicantType ApplicantType,
    GuaranteeCasePhase CurrentPhase,
    GuaranteeCaseStatus CurrentStatus,
    string? WorkflowInstanceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt,
    CompanyDto? Company,
    GuaranteeApplicationDto? Application = null,
    GuaranteeApprovalFormDto? ApprovalForm = null,
    GuaranteeApplicantCreditSnapshotDto? ApplicantCreditSnapshot = null,
    FundCreditCapacitySnapshotDto? FundCreditCapacity = null)
    : GuaranteeCaseDto(Id, CaseNumber, Title, ApplicantType, CurrentPhase, CurrentStatus, CreatedAt, UpdatedAt, CompletedAt);

/// <summary>جدول ۱ فرم تصویب — وضعیت اعتباری کل صندوق در بازه سقف فعال.</summary>
public sealed record GuaranteeApplicantCreditSnapshotDto(
    decimal? CreditLimitWithCheck,
    decimal? FundIssuedGuaranteesTotal,
    decimal? ActiveCommitments,
    decimal? RemainingCredit,
    DateOnly? PeriodStart,
    DateOnly? ExpiresAt);

public sealed record GuaranteeFundCreditLimitDto(
    decimal CreditLimitWithCheck,
    DateOnly PeriodStart,
    DateOnly ExpiresAt,
    decimal FundIssuedGuaranteesTotal,
    decimal ActiveCommitments,
    decimal? RemainingCredit,
    string? LastSetByUserId,
    string? LastSetByFullName,
    DateTimeOffset? UpdatedAt);

public sealed record GuaranteeApplicantCreditLimitDto(
    string ApplicantUserId,
    string? ApplicantFullName,
    Guid? CompanyId,
    string? CompanyName,
    decimal CreditLimitWithCheck,
    string? LastSetByUserId,
    string? LastSetByFullName,
    DateTimeOffset? UpdatedAt);

public sealed record GuaranteeApplicationDto(
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

public sealed record GuaranteeApprovalFormDto(
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

public sealed record GuaranteeCaseDocumentDto(
    Guid Id,
    GuaranteeDocumentType DocumentType,
    string FileName,
    string MimeType,
    long FileSize,
    int Version,
    DateTimeOffset UploadedAt);

public sealed record GuaranteeCaseCommentDto(
    Guid Id,
    GuaranteeCasePhase Phase,
    string SenderUserId,
    string? SenderFullName,
    string? SenderRole,
    string Message,
    bool IsRevisionRequest,
    bool IsInternal,
    DateTimeOffset CreatedAt);

public sealed record GuaranteeWorkflowHistoryDto(
    Guid Id,
    GuaranteeCasePhase FromPhase,
    GuaranteeCasePhase ToPhase,
    GuaranteeCaseStatus FromStatus,
    GuaranteeCaseStatus ToStatus,
    string ChangedByUserId,
    string? ChangedByFullName,
    string Action,
    string ActorRole,
    string? Comment,
    DateTimeOffset CreatedAt);

public sealed record GuaranteeRenewalDto(
    Guid Id,
    string CaseNumber,
    Guid ParentGuaranteeCaseId,
    string ParentCaseNumber,
    string? ParentBeneficiaryName,
    string? ParentCompanyName,
    string? ApplicantFullName,
    RenewalKind RenewalKind,
    GuaranteeRenewalStatus CurrentStatus,
    DateOnly? RequestedExpiryDate,
    decimal? RequestedAmount,
    DateOnly? ApprovedExpiryDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt);
