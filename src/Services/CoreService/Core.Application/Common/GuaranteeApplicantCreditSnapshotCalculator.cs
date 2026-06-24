using Core.Application.Abstractions;
using Core.Application.DTOs;
using Core.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Application.Common;

/// <summary>
/// جدول ۱ فرم تصویب — سقف فعال صندوق و جمع صادره/تعهدات فقط در بازه سقف جاری (از شروع سقف تا تاریخ انقضا).
/// </summary>
public static class GuaranteeApplicantCreditSnapshotCalculator
{
    public static Task<GuaranteeApplicantCreditSnapshotDto> ComputeAsync(
        ICoreUnitOfWork unitOfWork,
        GuaranteeCase current,
        CancellationToken cancellationToken)
        => ComputeFundSnapshotAsync(unitOfWork, current, cancellationToken);

    public static async Task<GuaranteeApplicantCreditSnapshotDto> ComputeFundSnapshotAsync(
        ICoreUnitOfWork unitOfWork,
        GuaranteeCase? currentCase,
        CancellationToken cancellationToken)
    {
        var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var capacity = await FundCreditLimitCapacityCalculator.ComputeActiveAsync(
            unitOfWork,
            FundModuleType.Guarantee,
            referenceDate,
            cancellationToken);

        if (capacity.TotalPeriodAllocation is null or <= 0)
            return new GuaranteeApplicantCreditSnapshotDto(null, null, null, null, null, null);

        var periodStart = capacity.PeriodStart!.Value;
        var expiresAt = capacity.ExpiresAt!.Value;
        var totalUtilized = capacity.TotalUtilized ?? 0m;
        var creditLimit = capacity.TotalPeriodAllocation.Value;

        var cases = await unitOfWork.GuaranteeCases.GetCreditProjectionsAsync(cancellationToken);

        var fundIssued = cases
            .Where(c => c.Status == GuaranteeCaseStatus.Completed && c.Amount is > 0)
            .Where(c => IsReferenceDateInPeriod(GetIssuedReferenceDate(c), periodStart, expiresAt))
            .Sum(c => c.Amount!.Value);

        var activeCommitments = totalUtilized - fundIssued;

        return new GuaranteeApplicantCreditSnapshotDto(
            creditLimit,
            fundIssued,
            activeCommitments,
            capacity.RemainingCapacity,
            periodStart,
            expiresAt);
    }

    public static async Task<FundCreditLimitSettings?> ResolveFundCreditLimitSettingsAsync(
        ICoreUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var pool = await FundCreditLimitCapacityCalculator.ResolveActivePoolAsync(
            unitOfWork,
            FundModuleType.Guarantee,
            referenceDate,
            cancellationToken);

        if (pool is null)
            return null;

        return new FundCreditLimitSettings(
            pool.CreditLimitWithCheck,
            pool.PeriodStart,
            pool.ExpiresAt);
    }

    public static async Task<decimal> ResolveFundCreditLimitAsync(
        ICoreUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var settings = await ResolveFundCreditLimitSettingsAsync(unitOfWork, cancellationToken);
        return settings?.CreditLimitWithCheck ?? 0m;
    }

    public static decimal ResolveCaseAmount(GuaranteeCase guaranteeCase)
    {
        var amount = guaranteeCase.ApprovalForm?.GuaranteeAmount
            ?? guaranteeCase.Application?.RequestedGuaranteeAmount;
        return amount is > 0 ? amount.Value : 0m;
    }

    private static bool IsReferenceDateInPeriod(DateOnly? date, DateOnly periodStart, DateOnly expiresAt)
        => date is not null && date.Value >= periodStart && date.Value <= expiresAt;

    private static DateOnly? GetIssuedReferenceDate(GuaranteeCaseCreditProjection c)
    {
        if (c.IssuanceDate is not null)
            return c.IssuanceDate;

        if (c.CompletedAt is not null)
            return DateOnly.FromDateTime(c.CompletedAt.Value.UtcDateTime);

        return null;
    }

    public sealed record FundCreditLimitSettings(
        decimal CreditLimitWithCheck,
        DateOnly PeriodStart,
        DateOnly ExpiresAt);
}
