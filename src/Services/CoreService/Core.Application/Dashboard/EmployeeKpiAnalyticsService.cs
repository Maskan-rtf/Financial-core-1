using System.Text.Json;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Core.Application.Dashboard;

public sealed class EmployeeKpiAnalyticsService(
    IDashboardStatsRepository statsRepository,
    IUserContext userContext,
    ILogger<EmployeeKpiAnalyticsService> logger) : IEmployeeKpiAnalyticsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(6);

    public async Task<Result<EmployeeKpiResponseDto>> GetEmployeeKpisAsync(
        EmployeeKpiPeriod period,
        CancellationToken cancellationToken = default)
    {
        if (!DashboardRoleResolver.CanViewEmployeeKpi(userContext.Roles))
            return Result<EmployeeKpiResponseDto>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var snapshot = await statsRepository.GetByKeyAsync(EmployeeKpiPeriodResolver.SnapshotKey, cancellationToken);
        if (snapshot is null)
        {
            logger.LogWarning("Employee KPI snapshot missing");
            return Result<EmployeeKpiResponseDto>.Ok(EmptyResponse(period));
        }

        EmployeeKpiCachePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<EmployeeKpiCachePayload>(snapshot.PayloadJson, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize employee KPI snapshot");
            return Result<EmployeeKpiResponseDto>.Fail(Error.Unexpected("Employee KPI cache is corrupted."));
        }

        var periodKey = EmployeeKpiPeriodResolver.ToApiValue(period);
        var periodData = payload?.Periods.FirstOrDefault(p =>
            string.Equals(p.Period, periodKey, StringComparison.OrdinalIgnoreCase));

        if (periodData is null)
        {
            var (start, end) = EmployeeKpiPeriodResolver.ResolveRange(period, snapshot.ComputedAtUtc);
            return Result<EmployeeKpiResponseDto>.Ok(new EmployeeKpiResponseDto
            {
                Period = periodKey,
                ComputedAtUtc = snapshot.ComputedAtUtc,
                PeriodStartUtc = start,
                PeriodEndUtc = end,
                IsStale = DateTimeOffset.UtcNow - snapshot.ComputedAtUtc > StaleThreshold,
                Departments = []
            });
        }

        return Result<EmployeeKpiResponseDto>.Ok(new EmployeeKpiResponseDto
        {
            Period = periodData.Period,
            ComputedAtUtc = snapshot.ComputedAtUtc,
            PeriodStartUtc = periodData.PeriodStartUtc,
            PeriodEndUtc = periodData.PeriodEndUtc,
            IsStale = DateTimeOffset.UtcNow - snapshot.ComputedAtUtc > StaleThreshold,
            Departments = EmployeeKpiDepartmentFilter.FilterForSlaDashboard(periodData.Departments)
        });
    }

    private static EmployeeKpiResponseDto EmptyResponse(EmployeeKpiPeriod period)
    {
        var now = DateTimeOffset.UtcNow;
        var (start, end) = EmployeeKpiPeriodResolver.ResolveRange(period, now);
        return new EmployeeKpiResponseDto
        {
            Period = EmployeeKpiPeriodResolver.ToApiValue(period),
            ComputedAtUtc = now,
            PeriodStartUtc = start,
            PeriodEndUtc = end,
            IsStale = true,
            Departments = []
        };
    }
}
