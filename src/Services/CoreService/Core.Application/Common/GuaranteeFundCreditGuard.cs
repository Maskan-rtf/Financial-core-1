using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Application.Common;

/// <summary>
/// کنترل سقف اعتبار صندوق هنگام ارسال فرم تصویب (وضعیت ۴).
/// </summary>
public static class GuaranteeFundCreditGuard
{
    public static async Task<Result> ValidateAmendmentExtensionAsync(
        ICoreUnitOfWork unitOfWork,
        GuaranteeCase caseEntity,
        CancellationToken cancellationToken)
    {
        var settings = await GuaranteeApplicantCreditSnapshotCalculator.ResolveFundCreditLimitSettingsAsync(
            unitOfWork,
            cancellationToken);

        if (settings is null || settings.CreditLimitWithCheck <= 0)
            return Result.Fail(Error.Conflict(ApiMessages.FundCreditLimitNotSet));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today > settings.ExpiresAt)
            return Result.Fail(Error.Conflict(ApiMessages.FundCreditLimitExpired));

        if (today < settings.PeriodStart)
            return Result.Fail(Error.Conflict(ApiMessages.FundCreditLimitNotYetActive));

        var requestAmount = GuaranteeApplicantCreditSnapshotCalculator.ResolveCaseAmount(caseEntity);
        if (requestAmount <= 0)
            return Result.Fail(Error.Conflict(ApiMessages.GuaranteeApprovalFormAmountRequired));

        return Result.Ok();
    }

    public static async Task<Result> ValidateApprovalFormSubmitAsync(
        ICoreUnitOfWork unitOfWork,
        GuaranteeCase caseEntity,
        CancellationToken cancellationToken)
    {
        var settings = await GuaranteeApplicantCreditSnapshotCalculator.ResolveFundCreditLimitSettingsAsync(
            unitOfWork,
            cancellationToken);

        if (settings is null || settings.CreditLimitWithCheck <= 0)
            return Result.Fail(Error.Conflict(ApiMessages.FundCreditLimitNotSet));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today > settings.ExpiresAt)
            return Result.Fail(Error.Conflict(ApiMessages.FundCreditLimitExpired));

        if (today < settings.PeriodStart)
            return Result.Fail(Error.Conflict(ApiMessages.FundCreditLimitNotYetActive));

        var requestAmount = GuaranteeApplicantCreditSnapshotCalculator.ResolveCaseAmount(caseEntity);
        if (requestAmount <= 0)
            return Result.Fail(Error.Conflict(ApiMessages.GuaranteeApprovalFormAmountRequired));

        var snapshot = await GuaranteeApplicantCreditSnapshotCalculator.ComputeFundSnapshotAsync(
            unitOfWork,
            caseEntity,
            cancellationToken);

        var fundIssued = snapshot.FundIssuedGuaranteesTotal ?? 0m;
        var active = snapshot.ActiveCommitments ?? 0m;
        var ceiling = settings.CreditLimitWithCheck;

        if (fundIssued + active > ceiling)
        {
            return Result.Fail(Error.Conflict(
                string.Format(
                    ApiMessages.FundCreditLimitExceededFormat,
                    fundIssued.ToString("N0"),
                    active.ToString("N0"),
                    ceiling.ToString("N0"))));
        }

        if (snapshot.RemainingCredit is < 0)
        {
            return Result.Fail(Error.Conflict(
                string.Format(
                    ApiMessages.FundCreditLimitExceededFormat,
                    fundIssued.ToString("N0"),
                    active.ToString("N0"),
                    ceiling.ToString("N0"))));
        }

        return Result.Ok();
    }
}
