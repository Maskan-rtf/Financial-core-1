using System.Text.Json.Serialization;
using Core.Application.Requests;
using Core.Domain.Enums;

namespace Core.Application.DTOs;

[JsonDerivedType(typeof(LoanCaseApplicantDto), typeDiscriminator: "applicant")]
[JsonDerivedType(typeof(LoanCaseInternalDto), typeDiscriminator: "internal")]
public abstract record LoanCaseDto(
    Guid Id,
    string CaseNumber,
    string? Title,
    ApplicantType ApplicantType,
    LoanCasePhase CurrentPhase,
    LoanCaseStatus CurrentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record LoanCaseApplicantDto(
    Guid Id,
    string CaseNumber,
    string? Title,
    ApplicantType ApplicantType,
    LoanCasePhase CurrentPhase,
    LoanCaseStatus CurrentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt,
    CompanyDto? Company,
    LoanApplicationDto? Application = null,
    LoanApprovalDetailDto? ApprovalDetail = null,
    IReadOnlyList<LoanInstallmentDto>? Installments = null,
    IReadOnlyList<LoanPaymentDto>? Payments = null,
    FundCreditCapacitySnapshotDto? FundCreditCapacity = null)
    : LoanCaseDto(Id, CaseNumber, Title, ApplicantType, CurrentPhase, CurrentStatus, CreatedAt, UpdatedAt, CompletedAt);

public sealed record LoanCaseInternalDto(
    Guid Id,
    string CaseNumber,
    string? Title,
    string ApplicantUserId,
    string? ApplicantFullName,
    string? ApplicantPhoneNumber,
    ApplicantType ApplicantType,
    LoanCasePhase CurrentPhase,
    LoanCaseStatus CurrentStatus,
    string? WorkflowInstanceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt,
    CompanyDto? Company,
    LoanApplicationDto? Application = null,
    LoanApprovalDetailDto? ApprovalDetail = null,
    IReadOnlyList<LoanInstallmentDto>? Installments = null,
    IReadOnlyList<LoanPaymentDto>? Payments = null,
    FundCreditCapacitySnapshotDto? FundCreditCapacity = null)
    : LoanCaseDto(Id, CaseNumber, Title, ApplicantType, CurrentPhase, CurrentStatus, CreatedAt, UpdatedAt, CompletedAt);

public sealed record LoanApplicationDto(
    decimal? RequestedAmount,
    string? RequestedAmountInWords,
    string? FacilitySubject,
    string? OfferedGuarantees,
    ApplicantCategory ApplicantCategory,
    string? ApplicantCategoryOther,
    string? RepresentativePosition);

public sealed record LoanApprovalDetailDto(
    decimal? DebtToAssetRatio,
    decimal? CurrentRatio,
    decimal? ProfitabilityRatioPercent,
    decimal? CreditLimitWithCheck,
    bool? IsCreditLineActive,
    decimal? RemainingCreditAfterGrant,
    LoanFacilityType? FacilityType,
    string? ContractSubject,
    string? BrokerageAndRelatedContract,
    decimal? ApprovedAmount,
    string? ApprovedAmountInWords,
    int? RepaymentMonths,
    int? GracePeriodMonths,
    decimal? AnnualProfitRatePercent,
    decimal? DailyPenaltyRatePercent,
    string? CollateralDescription,
    string? GuarantorsDescription,
    string? OtherNotes,
    decimal? ExpectedTotalProfit,
    decimal? RepaymentCheckAmount);

public sealed record LoanCaseDocumentDto(
    Guid Id,
    LoanDocumentType DocumentType,
    string FileName,
    string MimeType,
    long FileSize,
    int Version,
    string UploadedByUserId,
    string? UploadedByFullName,
    DateTimeOffset UploadedAt);

public sealed record LoanInstallmentDto(
    Guid Id,
    int RowNumber,
    DateOnly InstallmentDate,
    decimal PrincipalAmount,
    decimal ProfitAmount,
    decimal TotalAmount,
    decimal FundShareOfPrincipal,
    decimal FundShareOfProfit,
    decimal FundShareOfTotal,
    bool IsGracePeriod,
    bool IsPaid,
    DateTimeOffset? PaidAt);

public sealed record LoanPaymentDto(
    Guid Id,
    decimal Amount,
    DateOnly PaymentDate,
    string TransactionNumber,
    string? ReceiptS3Key,
    string? Notes,
    int StageNumber,
    string CreatedByUserId,
    string? CreatedByFullName,
    DateTimeOffset CreatedAt);

public sealed record LoanWorkflowHistoryDto(
    Guid Id,
    LoanCasePhase FromPhase,
    LoanCasePhase ToPhase,
    LoanCaseStatus FromStatus,
    LoanCaseStatus ToStatus,
    string ChangedByUserId,
    string? ChangedByFullName,
    string Action,
    string ActorRole,
    Guid CorrelationId,
    string? Comment,
    DateTimeOffset CreatedAt);

public sealed record LoanCaseCommentDto(
    Guid Id,
    LoanCasePhase Phase,
    string SenderUserId,
    string? SenderFullName,
    string? SenderRole,
    string Message,
    bool IsRevisionRequest,
    bool IsInternal,
    Guid? ParentId,
    DateTimeOffset CreatedAt,
    LoanCaseStatus? WorkflowStatusAtCreation = null,
    string? WorkflowStatusLabel = null);

public sealed record PresignLoanUploadResponse(string S3Key, string Url, DateTimeOffset ExpiresAt, int Version);
