using System.Text.Json;
using Core.Application.Abstractions;
using Core.Domain.Entities.Analytics;
using Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Dashboard;

public sealed class EmployeeKpiAggregationService(
    ICoreDbContext dbContext,
    IDashboardStatsRepository statsRepository,
    IUserDisplayLookup userDisplayLookup,
    ILogger<EmployeeKpiAggregationService> logger) : IEmployeeKpiAggregationService
{
    private const int MaxSamplesPerEmployee = 120;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly EmployeeKpiPeriod[] AllPeriods =
    [
        EmployeeKpiPeriod.Last30Days,
        EmployeeKpiPeriod.Last90Days,
        EmployeeKpiPeriod.ThisQuarter,
        EmployeeKpiPeriod.AllTime
    ];

    public async Task<DateTimeOffset> AggregateAsync(CancellationToken cancellationToken = default)
    {
        var computedAt = DateTimeOffset.UtcNow;
        logger.LogInformation("Employee KPI aggregation started at {ComputedAt}", computedAt);

        var segments = await LoadWorkflowSegmentsAsync(cancellationToken);
        var periods = AllPeriods
            .Select(period => BuildPeriodSnapshot(period, computedAt, segments))
            .ToList();

        var userIds = periods
            .SelectMany(p => p.Departments)
            .SelectMany(d => d.Employees)
            .Select(e => e.UserId);
        var userLookup = await userDisplayLookup.GetByIdsAsync(userIds, cancellationToken);
        periods = periods
            .Select(p => EnrichPeriodWithUserNames(p, userLookup))
            .ToList();

        var payload = new EmployeeKpiCachePayload { Periods = periods };

        await statsRepository.UpsertAsync(new DashboardStatsSnapshot
        {
            Id = Guid.NewGuid(),
            SnapshotKey = EmployeeKpiPeriodResolver.SnapshotKey,
            SnapshotType = DashboardSnapshotType.EmployeeKpi,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            ComputedAtUtc = computedAt
        }, cancellationToken);

        logger.LogInformation(
            "Employee KPI aggregation completed at {ComputedAt} with {SegmentCount} segments",
            computedAt,
            segments.Count);

        return computedAt;
    }

    private async Task<List<WorkflowSegment>> LoadWorkflowSegmentsAsync(CancellationToken ct)
    {
        var investmentRows = await dbContext.CaseWorkflowHistories.AsNoTracking()
            .Where(h => !h.Case.IsDeleted)
            .Select(h => new WorkflowHistoryRow(h.CaseId, h.ChangedByUserId, h.ActorRole, h.CreatedAt))
            .ToListAsync(ct);

        var guaranteeRows = await dbContext.GuaranteeCaseWorkflowHistories.AsNoTracking()
            .Where(h => !h.Case.IsDeleted)
            .Select(h => new WorkflowHistoryRow(h.CaseId, h.ChangedByUserId, h.ActorRole, h.CreatedAt))
            .ToListAsync(ct);

        var loanRows = await dbContext.LoanCaseWorkflowHistories.AsNoTracking()
            .Where(h => !h.Case.IsDeleted)
            .Select(h => new WorkflowHistoryRow(h.CaseId, h.ChangedByUserId, h.ActorRole, h.CreatedAt))
            .ToListAsync(ct);

        var segments = new List<WorkflowSegment>(investmentRows.Count + guaranteeRows.Count + loanRows.Count);
        ExtractSegments(investmentRows, segments);
        ExtractSegments(guaranteeRows, segments);
        ExtractSegments(loanRows, segments);
        return segments;
    }

    private static void ExtractSegments(IReadOnlyList<WorkflowHistoryRow> rows, List<WorkflowSegment> output)
    {
        foreach (var group in rows.GroupBy(r => r.CaseId))
        {
            var ordered = group.OrderBy(r => r.CreatedAt).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var current = ordered[i];
                if (!DashboardRoleResolver.IsInternalActorRole(current.ActorRole))
                    continue;

                var previous = ordered[i - 1];
                var hours = (current.CreatedAt - previous.CreatedAt).TotalHours;
                if (hours < 0)
                    continue;

                output.Add(new WorkflowSegment(
                    current.ChangedByUserId,
                    current.ActorRole,
                    current.CreatedAt,
                    hours));
            }
        }
    }

    private EmployeeKpiPeriodSnapshotDto BuildPeriodSnapshot(
        EmployeeKpiPeriod period,
        DateTimeOffset computedAt,
        IReadOnlyList<WorkflowSegment> segments)
    {
        var (start, end) = EmployeeKpiPeriodResolver.ResolveRange(period, computedAt);
        var filtered = segments
            .Where(s => s.ResolvedAt >= start && s.ResolvedAt <= end)
            .ToList();

        var grouped = filtered
            .GroupBy(s => new
            {
                DepartmentKey = DashboardRoleResolver.ResolveDepartmentKeyFromActorRole(s.ActorRole),
                s.UserId
            })
            .Select(g =>
            {
                var hours = g.Select(x => x.ResolutionHours).ToList();
                return new
                {
                    g.Key.DepartmentKey,
                    g.Key.UserId,
                    Hours = hours,
                    Count = hours.Count,
                    Avg = hours.Count == 0 ? 0 : hours.Average(),
                    Min = hours.Count == 0 ? 0 : hours.Min(),
                    Max = hours.Count == 0 ? 0 : hours.Max()
                };
            })
            .ToList();

        var departments = grouped
            .GroupBy(x => x.DepartmentKey)
            .Select(deptGroup => new DepartmentEmployeeKpiDto
            {
                DepartmentKey = deptGroup.Key,
                DepartmentTitle = ResolveDepartmentTitle(deptGroup.Key),
                Employees = deptGroup
                    .Select(emp => new EmployeeKpiMetricsDto
                    {
                        UserId = emp.UserId,
                        AverageResolutionHours = Math.Round(emp.Avg, 2),
                        AverageResolutionDays = Math.Round(emp.Avg / 24.0, 2),
                        MinResolutionHours = Math.Round(emp.Min, 2),
                        MaxResolutionHours = Math.Round(emp.Max, 2),
                        TotalTasksResolved = emp.Count,
                        ResolutionHoursSamples = emp.Hours
                            .OrderByDescending(h => h)
                            .Take(MaxSamplesPerEmployee)
                            .Select(h => Math.Round(h, 2))
                            .ToList()
                    })
                    .OrderByDescending(e => e.TotalTasksResolved)
                    .ThenBy(e => e.UserId)
                    .ToList()
            })
            .Where(d => EmployeeKpiDepartmentFilter.IncludeInSlaDashboard(d.DepartmentKey))
            .OrderBy(d => d.DepartmentKey)
            .ToList();

        return new EmployeeKpiPeriodSnapshotDto
        {
            Period = EmployeeKpiPeriodResolver.ToApiValue(period),
            PeriodStartUtc = start,
            PeriodEndUtc = end,
            Departments = departments
        };
    }

    private static string ResolveDepartmentTitle(string departmentKey) => departmentKey switch
    {
        "Management" => "مدیریت",
        "Other" => "سایر",
        _ => DashboardRoleResolver.GetDepartmentTitle(departmentKey)
    };

    private static EmployeeKpiPeriodSnapshotDto EnrichPeriodWithUserNames(
        EmployeeKpiPeriodSnapshotDto period,
        IReadOnlyDictionary<string, Core.Application.DTOs.UserDisplayDto> userLookup)
    {
        var departments = period.Departments
            .Select(dept => dept with
            {
                Employees = dept.Employees
                    .Select(emp => emp with
                    {
                        FullName = userLookup.TryGetValue(emp.UserId, out var display)
                            ? display.FullName
                            : null
                    })
                    .OrderByDescending(e => e.TotalTasksResolved)
                    .ThenBy(e => e.FullName ?? e.UserId)
                    .ToList()
            })
            .ToList();

        return period with { Departments = departments };
    }

    private sealed record WorkflowHistoryRow(Guid CaseId, string ChangedByUserId, string ActorRole, DateTimeOffset CreatedAt);

    private sealed record WorkflowSegment(string UserId, string ActorRole, DateTimeOffset ResolvedAt, double ResolutionHours);
}
