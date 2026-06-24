using Core.Application.Abstractions;
using Core.Application.DTOs;
using Core.Domain.Enums;

namespace Core.Application.Common;

/// <summary>
/// محاسبه سقف دوره‌ای و مصرف اعتبار صندوق برای ماژول‌های ضمانت‌نامه و تسهیلات.
/// </summary>
public static class FundCreditLimitCapacityCalculator
{
    private static readonly GuaranteeCaseStatus[] GuaranteeActiveCommitmentStatuses =
    [
        GuaranteeCaseStatus.CreditReview,
        GuaranteeCaseStatus.ApprovalFormEntry,
        GuaranteeCaseStatus.CeoApprovalInitial,
        GuaranteeCaseStatus.WaitingDraftContract,
        GuaranteeCaseStatus.WaitingSignedContractAndAttachments,
        GuaranteeCaseStatus.FinancialAttachmentReview,
        GuaranteeCaseStatus.WaitingFinalContract,
        GuaranteeCaseStatus.CeoApprovalFinal,
        GuaranteeCaseStatus.WaitingIssuanceDocuments,
    ];

    private static readonly LoanCaseStatus[] LoanActivePipelineStatuses =
    [
        LoanCaseStatus.PendingCeoInitialApproval,
        LoanCaseStatus.PendingLegalRawContract,
        LoanCaseStatus.PendingApplicantSignature,
        LoanCaseStatus.PendingLegalFinalReview,
        LoanCaseStatus.RevisionRequestedByLegal,
        LoanCaseStatus.PendingFinancialReview,
        LoanCaseStatus.RevisionRequestedByFinancial,
        LoanCaseStatus.PendingLegalFinalContract,
        LoanCaseStatus.PendingCeoFinalApproval,
        LoanCaseStatus.ReadyForPayment,
    ];

    private static readonly LoanCaseStatus[] LoanDisbursedStatuses =
    [
        LoanCaseStatus.RepaymentPhase,
        LoanCaseStatus.Completed,
        LoanCaseStatus.Archived,
    ];

    public static async Task<FundCreditCapacitySnapshotDto> ComputeActiveAsync(
        ICoreUnitOfWork unitOfWork,
        FundModuleType moduleType,
        DateOnly referenceDate,
        CancellationToken cancellationToken)
    {
        var pool = await unitOfWork.FundCreditLimits.ResolveActivePoolAsync(moduleType, referenceDate, cancellationToken);
        if (pool is null)
            return new FundCreditCapacitySnapshotDto(moduleType, null, null, null, null, null);

        var utilization = await ComputeUtilizationAsync(
            unitOfWork,
            moduleType,
            pool.PeriodStart,
            pool.ExpiresAt,
            cancellationToken);
        return new FundCreditCapacitySnapshotDto(
            moduleType,
            pool.CreditLimitWithCheck,
            utilization,
            pool.CreditLimitWithCheck - utilization,
            pool.PeriodStart,
            pool.ExpiresAt);
    }

    public static async Task<FundCreditCapacitySnapshotDto> ComputeForPoolAsync(
        ICoreUnitOfWork unitOfWork,
        Guid poolId,
        CancellationToken cancellationToken)
    {
        var pool = await unitOfWork.FundCreditLimits.GetPoolProjectionAsync(poolId, cancellationToken);

        if (pool is null)
            return new FundCreditCapacitySnapshotDto(FundModuleType.Guarantee, null, null, null, null, null);

        var utilization = await ComputeUtilizationAsync(
            unitOfWork,
            pool.ModuleType,
            pool.PeriodStart,
            pool.ExpiresAt,
            cancellationToken);
        return new FundCreditCapacitySnapshotDto(
            pool.ModuleType,
            pool.CreditLimitWithCheck,
            utilization,
            pool.CreditLimitWithCheck - utilization,
            pool.PeriodStart,
            pool.ExpiresAt);
    }

    public static Task<FundCreditLimitPoolProjection?> ResolveActivePoolAsync(
        ICoreUnitOfWork unitOfWork,
        FundModuleType moduleType,
        DateOnly referenceDate,
        CancellationToken cancellationToken)
        => unitOfWork.FundCreditLimits.ResolveActivePoolAsync(moduleType, referenceDate, cancellationToken);

    public static async Task<decimal> ComputeUtilizationAsync(
        ICoreUnitOfWork unitOfWork,
        FundModuleType moduleType,
        DateOnly periodStart,
        DateOnly expiresAt,
        CancellationToken cancellationToken)
        => moduleType switch
        {
            FundModuleType.Guarantee => await ComputeGuaranteeUtilizationAsync(
                unitOfWork,
                periodStart,
                expiresAt,
                cancellationToken),
            FundModuleType.Loan => await ComputeLoanUtilizationAsync(
                unitOfWork,
                periodStart,
                expiresAt,
                cancellationToken),
            _ => 0m
        };

    public static Task<bool> HasOverlappingPeriodAsync(
        ICoreUnitOfWork unitOfWork,
        FundModuleType moduleType,
        DateOnly periodStart,
        DateOnly expiresAt,
        Guid? excludeId,
        CancellationToken cancellationToken)
        => unitOfWork.FundCreditLimits.HasOverlappingPeriodAsync(
            moduleType,
            periodStart,
            expiresAt,
            excludeId,
            cancellationToken);

    private static async Task<decimal> ComputeGuaranteeUtilizationAsync(
        ICoreUnitOfWork unitOfWork,
        DateOnly periodStart,
        DateOnly expiresAt,
        CancellationToken cancellationToken)
    {
        var cases = await unitOfWork.GuaranteeCases.GetCreditProjectionsAsync(cancellationToken);

        var issued = cases
            .Where(c => c.Status == GuaranteeCaseStatus.Completed && c.Amount is > 0)
            .Where(c => IsReferenceDateInPeriod(GetGuaranteeIssuedReferenceDate(c), periodStart, expiresAt))
            .Sum(c => c.Amount!.Value);

        var active = cases
            .Where(c => GuaranteeActiveCommitmentStatuses.Contains(c.Status) && c.Amount is > 0)
            .Where(c => IsCaseCreatedInPeriod(c.CreatedAt, periodStart, expiresAt))
            .Sum(c => c.Amount!.Value);

        return issued + active;
    }

    private static async Task<decimal> ComputeLoanUtilizationAsync(
        ICoreUnitOfWork unitOfWork,
        DateOnly periodStart,
        DateOnly expiresAt,
        CancellationToken cancellationToken)
    {
        var cases = await unitOfWork.LoanCases.GetCreditProjectionsAsync(cancellationToken);

        var disbursed = cases
            .Where(c => LoanDisbursedStatuses.Contains(c.Status) && c.Amount is > 0)
            .Where(c => IsReferenceDateInPeriod(GetLoanDisbursedReferenceDate(c), periodStart, expiresAt))
            .Sum(c => c.Amount!.Value);

        var active = cases
            .Where(c => LoanActivePipelineStatuses.Contains(c.Status) && c.Amount is > 0)
            .Where(c => IsCaseCreatedInPeriod(c.CreatedAt, periodStart, expiresAt))
            .Sum(c => c.Amount!.Value);

        return disbursed + active;
    }

    private static bool IsReferenceDateInPeriod(DateOnly? date, DateOnly periodStart, DateOnly expiresAt)
        => date is not null && date.Value >= periodStart && date.Value <= expiresAt;

    private static bool IsCaseCreatedInPeriod(DateTimeOffset createdAt, DateOnly periodStart, DateOnly expiresAt)
    {
        var created = DateOnly.FromDateTime(createdAt.UtcDateTime);
        return created >= periodStart && created <= expiresAt;
    }

    private static DateOnly? GetGuaranteeIssuedReferenceDate(GuaranteeCaseCreditProjection c)
    {
        if (c.IssuanceDate is not null)
            return c.IssuanceDate;

        if (c.CompletedAt is not null)
            return DateOnly.FromDateTime(c.CompletedAt.Value.UtcDateTime);

        return null;
    }

    private static DateOnly? GetLoanDisbursedReferenceDate(LoanCaseCreditProjection c)
    {
        if (c.CompletedAt is not null)
            return DateOnly.FromDateTime(c.CompletedAt.Value.UtcDateTime);

        return null;
    }
}
