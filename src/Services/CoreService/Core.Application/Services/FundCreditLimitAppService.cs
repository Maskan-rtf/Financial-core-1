using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Persistence.Queries;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.DTOs;
using Core.Application.Mappers;
using Core.Application.Requests;
using Core.Domain.Entities.Fund;
using Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Core.Application.Services;

public sealed class FundCreditLimitAppService(
    ICoreDbContext dbContext,
    IUserContext userContext,
    IUserDisplayLookup userDisplayLookup,
    IFundCreditLimitDtoMapper fundCreditLimitDtoMapper) : IFundCreditLimitAppService
{
    public async Task<Result<FundCreditLimitDto>> CreateAsync(CreateFundCreditLimitRequest request, CancellationToken ct)
    {
        if (!FundCreditLimitAuthorization.CanAccessFundCreditLimits(userContext.Roles))
            return Result<FundCreditLimitDto>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result<FundCreditLimitDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        var amountValidation = ValidateAmount(request.CreditLimitWithCheck);
        if (amountValidation.IsFailure)
            return Result<FundCreditLimitDto>.Fail(amountValidation.Error!);

        if (request.ExpiresAt < request.PeriodStart)
            return Result<FundCreditLimitDto>.Fail(Error.Validation(ApiMessages.InvalidFundCreditLimitPeriod));

        if (await FundCreditLimitCapacityCalculator.HasOverlappingPeriodAsync(
                dbContext,
                request.ModuleType,
                request.PeriodStart,
                request.ExpiresAt,
                excludeId: null,
                ct))
        {
            return Result<FundCreditLimitDto>.Fail(Error.Conflict(ApiMessages.FundCreditLimitPeriodOverlap));
        }

        var entity = new FundCreditLimit(
            request.ModuleType,
            request.CreditLimitWithCheck,
            request.PeriodStart,
            request.ExpiresAt,
            userId);

        await dbContext.FundCreditLimits.AddAsync(entity, ct);
        await dbContext.SaveChangesAsync(ct);

        var userLookup = await userDisplayLookup.GetByIdsAsync([userId], ct);
        return Result<FundCreditLimitDto>.Ok(await MapDtoAsync(entity, userLookup, ct));
    }

    public async Task<Result<FundCreditLimitDto>> UpdateAsync(Guid id, UpdateFundCreditLimitRequest request, CancellationToken ct)
    {
        if (!FundCreditLimitAuthorization.CanAccessFundCreditLimits(userContext.Roles))
            return Result<FundCreditLimitDto>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result<FundCreditLimitDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        var amountValidation = ValidateAmount(request.CreditLimitWithCheck);
        if (amountValidation.IsFailure)
            return Result<FundCreditLimitDto>.Fail(amountValidation.Error!);

        if (request.ExpiresAt < request.PeriodStart)
            return Result<FundCreditLimitDto>.Fail(Error.Validation(ApiMessages.InvalidFundCreditLimitPeriod));

        var entity = await dbContext.FundCreditLimits.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return Result<FundCreditLimitDto>.Fail(Error.NotFound(ApiMessages.FundCreditLimitNotFound));

        if (await FundCreditLimitCapacityCalculator.HasOverlappingPeriodAsync(
                dbContext,
                entity.ModuleType,
                request.PeriodStart,
                request.ExpiresAt,
                excludeId: id,
                ct))
        {
            return Result<FundCreditLimitDto>.Fail(Error.Conflict(ApiMessages.FundCreditLimitPeriodOverlap));
        }

        var utilization = await FundCreditLimitCapacityCalculator.ComputeUtilizationAsync(
            dbContext,
            entity.ModuleType,
            request.PeriodStart,
            request.ExpiresAt,
            ct);

        if (request.CreditLimitWithCheck < utilization)
        {
            return Result<FundCreditLimitDto>.Fail(Error.Validation(
                string.Format(ApiMessages.FundCreditLimitBelowUtilization, utilization.ToString("N0"))));
        }

        entity.Update(request.CreditLimitWithCheck, request.PeriodStart, request.ExpiresAt, userId);
        await dbContext.SaveChangesAsync(ct);

        var userLookup = await userDisplayLookup.GetByIdsAsync([userId], ct);
        return Result<FundCreditLimitDto>.Ok(await MapDtoAsync(entity, userLookup, ct));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (!FundCreditLimitAuthorization.CanAccessFundCreditLimits(userContext.Roles))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await dbContext.FundCreditLimits.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.FundCreditLimitNotFound));

        var utilization = await FundCreditLimitCapacityCalculator.ComputeUtilizationAsync(
            dbContext,
            entity.ModuleType,
            entity.PeriodStart,
            entity.ExpiresAt,
            ct);

        if (utilization > 0)
            return Result.Fail(Error.Conflict(ApiMessages.FundCreditLimitHasUtilization));

        dbContext.FundCreditLimits.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<PagedResult<FundCreditLimitDto>>> ListAsync(GetFundCreditLimitsRequest request, CancellationToken ct)
    {
        if (!FundCreditLimitAuthorization.CanAccessFundCreditLimits(userContext.Roles))
            return Result<PagedResult<FundCreditLimitDto>>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var page = await dbContext.FundCreditLimits
            .AsNoTracking()
            .OrderByDescending(x => x.ModuleType)
            .ThenByDescending(x => x.PeriodStart)
            .ToPagedResultAsync(request.NormalizedSkip, request.NormalizedTake, ct);

        var userLookup = await userDisplayLookup.GetByIdsAsync(
            page.Items.Select(x => x.LastSetByUserId).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!),
            ct);
        var items = new List<FundCreditLimitDto>(page.Items.Count);
        foreach (var row in page.Items)
            items.Add(await MapDtoAsync(row, userLookup, ct));

        return Result<PagedResult<FundCreditLimitDto>>.Ok(
            new PagedResult<FundCreditLimitDto>(items, page.Skip, page.Take, page.TotalCount));
    }

    public async Task<Result<FundCreditLimitDashboardSectionDto>> GetDashboardSectionAsync(CancellationToken ct)
    {
        if (!FundCreditLimitAuthorization.CanAccessFundCreditLimits(userContext.Roles))
            return Result<FundCreditLimitDashboardSectionDto>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var rows = await dbContext.FundCreditLimits
            .AsNoTracking()
            .OrderByDescending(x => x.ModuleType)
            .ThenByDescending(x => x.PeriodStart)
            .ToListAsync(ct);

        var userLookup = await userDisplayLookup.GetByIdsAsync(
            rows.Select(x => x.LastSetByUserId).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!),
            ct);
        var all = new List<FundCreditLimitDto>(rows.Count);
        foreach (var row in rows)
            all.Add(await MapDtoAsync(row, userLookup, ct));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var active = all
            .Where(x => x.PeriodStart <= today && x.ExpiresAt >= today)
            .ToList();

        return Result<FundCreditLimitDashboardSectionDto>.Ok(new FundCreditLimitDashboardSectionDto(active, all));
    }

    private static Result ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            return Result.Fail(Error.Validation(ApiMessages.InvalidCreditLimitAmount));

        if (amount > GuaranteeFundCreditLimits.MaxCreditLimitWithCheck)
            return Result.Fail(Error.Validation(ApiMessages.CreditLimitAmountTooLarge));

        return Result.Ok();
    }

    private async Task<FundCreditLimitDto> MapDtoAsync(
        FundCreditLimit row,
        IReadOnlyDictionary<string, UserDisplayDto> userLookup,
        CancellationToken ct)
    {
        var utilized = await FundCreditLimitCapacityCalculator.ComputeUtilizationAsync(
            dbContext,
            row.ModuleType,
            row.PeriodStart,
            row.ExpiresAt,
            ct);

        return fundCreditLimitDtoMapper.Map(
            row,
            utilized,
            userDisplayLookup.ResolveFullName(userLookup, row.LastSetByUserId));
    }
}
