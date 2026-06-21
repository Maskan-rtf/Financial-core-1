using Core.Domain.Enums;

namespace Core.Application.Requests;

public sealed record CreateLoanCaseRequest(ApplicantType ApplicantType, Guid? CompanyId, string? Title = null);

public sealed record UpdateLoanApplicationRequest(
    decimal? RequestedAmount,
    string? RequestedAmountInWords,
    string? FacilitySubject,
    string? OfferedGuarantees,
    ApplicantCategory ApplicantCategory,
    string? ApplicantCategoryOther,
    string? RepresentativePosition);

public sealed record UpdateLoanApprovalDetailRequest(
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

public sealed record PresignLoanUploadRequest(
    LoanDocumentType DocumentType,
    string FileName,
    string MimeType,
    long FileSize);

public sealed record LoanCaseSearchRequest(
    string? CaseNumber,
    string? ApplicantUserId,
    LoanCasePhase? Phase,
    LoanCaseStatus? Status,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Page = 1,
    int PageSize = 20);

public sealed record UpsertLoanInstallmentItemRequest(
    int RowNumber,
    DateOnly InstallmentDate,
    decimal PrincipalAmount,
    decimal ProfitAmount,
    decimal TotalAmount,
    decimal FundShareOfPrincipal,
    decimal FundShareOfProfit,
    decimal FundShareOfTotal,
    bool IsGracePeriod);

public sealed record UpsertLoanInstallmentsRequest(IReadOnlyList<UpsertLoanInstallmentItemRequest> Installments);

public sealed record RegisterLoanPaymentRequest(
    decimal Amount,
    DateOnly PaymentDate,
    string TransactionNumber,
    string? ReceiptS3Key,
    string? Notes,
    int StageNumber);

public sealed record MarkLoanInstallmentPaidRequest(
    DateOnly? PaidDate,
    decimal Amount,
    string TransactionNumber,
    string? ReceiptS3Key,
    string? Notes);
