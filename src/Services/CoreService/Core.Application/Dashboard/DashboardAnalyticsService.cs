using System.Text.Json;

using BuildingBlocks.Application.Abstractions;

using BuildingBlocks.Application.Errors;

using BuildingBlocks.Application.Results;

using BuildingBlocks.Domain.Abstractions;

using Core.Application.Abstractions;

using Core.Application.Common;

using Core.Application.DTOs;

using Core.Application.Kanban;

using Core.Application.Logging;

using Core.Domain.Enums;

using Microsoft.Extensions.Logging;



namespace Core.Application.Dashboard;



public sealed class DashboardAnalyticsService(

    IDashboardStatsRepository statsRepository,

    IKanbanAppService kanbanAppService,

    IFundCreditLimitAppService fundCreditLimitAppService,

    IUserContext userContext,

    ILogger<DashboardAnalyticsService> logger) : IDashboardAnalyticsService

{

    #region Configuration

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(6);



    #endregion

    #region Public API



    public async Task<Result<RoleDashboardResponse>> GetMyDashboardAsync(CancellationToken cancellationToken = default)

    {

        var userId = userContext.UserId;

        if (string.IsNullOrWhiteSpace(userId))

            return Result<RoleDashboardResponse>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));



        var viewKind = DashboardRoleResolver.ResolveViewKind(userContext.Roles);



        var fundCreditLimits = await LoadFundCreditLimitsSectionAsync(cancellationToken);



        var result = viewKind switch

        {

            DashboardViewKind.Executive => await BuildExecutiveResponseAsync(cancellationToken),

            DashboardViewKind.Department => await BuildDepartmentResponseAsync(departmentKey: null, cancellationToken),

            _ => await BuildApplicantResponseAsync(userId, cancellationToken)

        };



        if (result.IsFailure)

            return result;



        return Result<RoleDashboardResponse>.Ok(result.Value! with { FundCreditLimits = fundCreditLimits });

    }



    public async Task<Result<CeoDashboardDto>> GetCeoDashboardAsync(CancellationToken cancellationToken = default)

    {

        var executive = await LoadExecutiveCacheAsync(cancellationToken);

        if (executive is null)

            return Result<CeoDashboardDto>.Fail(Error.NotFound("داده داشبورد هنوز آماده نشده است. لطفاً چند دقیقه بعد تلاش کنید."));



        return Result<CeoDashboardDto>.Ok(BuildCeoDashboard(executive.Data));

    }



    public async Task<Result<BoardDashboardDto>> GetBoardDashboardAsync(CancellationToken cancellationToken = default)

    {

        var executive = await LoadExecutiveCacheAsync(cancellationToken);

        if (executive is null)

            return Result<BoardDashboardDto>.Fail(Error.NotFound("داده داشبورد هنوز آماده نشده است. لطفاً چند دقیقه بعد تلاش کنید."));



        return Result<BoardDashboardDto>.Ok(BuildBoardDashboard(executive.Data));

    }



    public async Task<Result<DepartmentDashboardViewDto>> GetDepartmentDashboardAsync(

        string? departmentKey = null,

        CancellationToken cancellationToken = default)

    {

        var response = await BuildDepartmentResponseAsync(departmentKey, cancellationToken);

        if (response.IsFailure || response.Value?.Department is null)

            return Result<DepartmentDashboardViewDto>.Fail(response.Error ?? Error.NotFound("داشبورد واحد یافت نشد."));



        return Result<DepartmentDashboardViewDto>.Ok(response.Value.Department);

    }



    public async Task<Result<ApplicantDashboardViewDto>> GetApplicantDashboardAsync(CancellationToken cancellationToken = default)

    {

        var userId = userContext.UserId;

        if (string.IsNullOrWhiteSpace(userId))

            return Result<ApplicantDashboardViewDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));



        var response = await BuildApplicantResponseAsync(userId, cancellationToken);

        if (response.IsFailure || response.Value?.Applicant is null)

            return Result<ApplicantDashboardViewDto>.Fail(response.Error ?? Error.NotFound("داشبورد متقاضی یافت نشد."));



        return Result<ApplicantDashboardViewDto>.Ok(response.Value.Applicant);

    }



    public async Task<Result<AdminDashboardOverviewDto>> GetAdminOverviewAsync(CancellationToken cancellationToken = default)

    {

        var executive = await LoadExecutiveCacheAsync(cancellationToken);

        if (executive is null)

            return Result<AdminDashboardOverviewDto>.Fail(Error.NotFound("داده داشبورد هنوز آماده نشده است. لطفاً چند دقیقه بعد تلاش کنید."));



        var snapshot = await statsRepository.GetByKeyAsync(DashboardRoleResolver.ExecutiveSnapshotKey, cancellationToken);

        var d = executive.Data;

        var fundCreditLimits = await LoadFundCreditLimitsSectionAsync(cancellationToken);

        var executiveData = d with { FundCreditLimits = fundCreditLimits };



        var departments = new List<DepartmentDashboardViewDto>();

        foreach (var key in DashboardRoleResolver.DepartmentKeys)

        {

            var deptSnapshot = await statsRepository.GetByKeyAsync(

                DashboardRoleResolver.DepartmentSnapshotKey(key),

                cancellationToken);

            if (deptSnapshot is null)

                continue;



            var cached = JsonSerializer.Deserialize<DepartmentStatsCachePayload>(deptSnapshot.PayloadJson, JsonOptions);

            if (cached is null)

                continue;



            var filteredQueue = DashboardDepartmentModuleFilter.FilterQueue(cached.DepartmentKey, cached.QueueByModule);

            var filteredModules = DashboardDepartmentModuleFilter.FilterModules(cached.DepartmentKey, cached.Modules);



            departments.Add(new DepartmentDashboardViewDto

            {

                DepartmentKey = cached.DepartmentKey,

                DepartmentTitle = cached.DepartmentTitle,

                TotalQueueCount = filteredQueue.Sum(x => x.Count),

                QueueByModule = filteredQueue,

                Modules = filteredModules,

                SpecificMetrics = cached.SpecificMetrics,

                RevisionRatePercent = cached.RevisionRatePercent,

                InboxQuickLinks = []

            });

        }



        var applicantSummary = await BuildApplicantOverviewSummaryAsync(cancellationToken);



        return Result<AdminDashboardOverviewDto>.Ok(new AdminDashboardOverviewDto

        {

            ComputedAtUtc = snapshot?.ComputedAtUtc,

            IsStale = IsStale(snapshot?.ComputedAtUtc),

            Modules = d.Modules,

            SystemHealth = d.SystemHealth,

            Executive = executiveData,

            Ceo = BuildCeoDashboard(d),

            Board = BuildBoardDashboard(d),

            Departments = departments,

            ApplicantSummary = applicantSummary,

            FundCreditLimits = fundCreditLimits

        });

    }



    #endregion

    #region Private



    private async Task<Result<RoleDashboardResponse>> BuildExecutiveResponseAsync(CancellationToken ct)

    {

        var executive = await LoadExecutiveCacheAsync(ct);

        if (executive is null)

            return Result<RoleDashboardResponse>.Fail(Error.NotFound("داده داشبورد هنوز آماده نشده است. لطفاً چند دقیقه بعد تلاش کنید."));



        var snapshot = await statsRepository.GetByKeyAsync(DashboardRoleResolver.ExecutiveSnapshotKey, ct);

        var fundCreditLimits = await LoadFundCreditLimitsSectionAsync(ct);

        var executiveData = executive.Data with { FundCreditLimits = fundCreditLimits };



        return Result<RoleDashboardResponse>.Ok(new RoleDashboardResponse

        {

            ViewType = "Executive",

            ComputedAtUtc = snapshot?.ComputedAtUtc,

            IsStale = IsStale(snapshot?.ComputedAtUtc),

            Executive = executiveData,

            FundCreditLimits = fundCreditLimits

        });

    }



    private async Task<Result<RoleDashboardResponse>> BuildDepartmentResponseAsync(string? departmentKey, CancellationToken ct)

    {

        var resolvedKey = ResolveDepartmentKeyForRequest(departmentKey);

        if (resolvedKey is null)

            return Result<RoleDashboardResponse>.Fail(Error.Forbidden("نقش شما برای داشبورد واحد تعریف نشده است."));



        var snapshotKey = DashboardRoleResolver.DepartmentSnapshotKey(resolvedKey);

        var snapshot = await statsRepository.GetByKeyAsync(snapshotKey, ct);

        if (snapshot is null)

            return Result<RoleDashboardResponse>.Fail(Error.NotFound("داده داشبورد واحد هنوز آماده نشده است."));



        var cached = JsonSerializer.Deserialize<DepartmentStatsCachePayload>(snapshot.PayloadJson, JsonOptions);

        if (cached is null)

            return Result<RoleDashboardResponse>.Fail(Error.NotFound("داده داشبورد واحد نامعتبر است."));



        var inboxLinks = departmentKey is null || resolvedKey == DashboardRoleResolver.ResolveDepartmentKey(userContext.Roles)

            ? await LoadInboxQuickLinksAsync(resolvedKey, ct)

            : [];



        var filteredQueue = DashboardDepartmentModuleFilter.FilterQueue(resolvedKey, cached.QueueByModule);

        var filteredModules = DashboardDepartmentModuleFilter.FilterModules(resolvedKey, cached.Modules);



        return Result<RoleDashboardResponse>.Ok(new RoleDashboardResponse

        {

            ViewType = "Department",

            ComputedAtUtc = snapshot.ComputedAtUtc,

            IsStale = IsStale(snapshot.ComputedAtUtc),

            Department = new DepartmentDashboardViewDto

            {

                DepartmentKey = cached.DepartmentKey,

                DepartmentTitle = cached.DepartmentTitle,

                TotalQueueCount = filteredQueue.Sum(x => x.Count),

                QueueByModule = filteredQueue,

                Modules = filteredModules,

                SpecificMetrics = cached.SpecificMetrics,

                RevisionRatePercent = cached.RevisionRatePercent,

                InboxQuickLinks = inboxLinks

            }

        });

    }



    private string? ResolveDepartmentKeyForRequest(string? requestedDepartmentKey)

    {

        if (!string.IsNullOrWhiteSpace(requestedDepartmentKey))

        {

            if (!DashboardRoleResolver.IsAdmin(userContext.Roles))

                return DashboardRoleResolver.ResolveDepartmentKey(userContext.Roles);



            var normalized = requestedDepartmentKey.Trim();

            return DashboardRoleResolver.DepartmentKeys.Any(k =>

                k.Equals(normalized, StringComparison.OrdinalIgnoreCase))

                ? DashboardRoleResolver.DepartmentKeys.First(k =>

                    k.Equals(normalized, StringComparison.OrdinalIgnoreCase))

                : null;

        }



        return DashboardRoleResolver.ResolveDepartmentKey(userContext.Roles);

    }



    private async Task<Result<RoleDashboardResponse>> BuildApplicantResponseAsync(string userId, CancellationToken ct)

    {

        var snapshotKey = DashboardRoleResolver.ApplicantSnapshotKey(userId);

        var snapshot = await statsRepository.GetByKeyAsync(snapshotKey, ct);

        if (snapshot is null)

        {

            ApplicationLog.Blocked(logger, "GetApplicantDashboard", "no cached snapshot for user", userId);

            return Result<RoleDashboardResponse>.Ok(new RoleDashboardResponse

            {

                ViewType = "Applicant",

                IsStale = true,

                Applicant = new ApplicantDashboardViewDto()

            });

        }



        var cached = JsonSerializer.Deserialize<ApplicantStatsCachePayload>(snapshot.PayloadJson, JsonOptions);

        if (cached is null)

            return Result<RoleDashboardResponse>.Fail(Error.NotFound("داده داشبورد متقاضی نامعتبر است."));



        return Result<RoleDashboardResponse>.Ok(new RoleDashboardResponse

        {

            ViewType = "Applicant",

            ComputedAtUtc = snapshot.ComputedAtUtc,

            IsStale = IsStale(snapshot.ComputedAtUtc),

            Applicant = new ApplicantDashboardViewDto

            {

                ActiveCases = cached.ActiveCases,

                PendingActionsCount = cached.PendingActionsCount,

                TotalRemainingDebt = cached.TotalRemainingDebt,

                UnpaidInstallmentsCount = cached.UnpaidInstallmentsCount,

                RecentComments = cached.RecentComments

            }

        });

    }



    private static CeoDashboardDto BuildCeoDashboard(ExecutiveDashboardViewDto d)

    {

        var modules = d.Modules;

        var totalRisk = modules.Sum(m => m.ActiveVolume);



        return new CeoDashboardDto

        {

            Modules = modules,

            TotalRiskExposure = totalRisk,

            DepartmentBottlenecks = d.DepartmentBottlenecks,

            PipelineByStatus = d.PipelineByStatus,

            TotalRequestedAmount = d.TotalRequestedAmount,

            ApprovedPaymentsSum = d.ApprovedPaymentsSum,

            CasesThisMonth = d.CasesThisMonth,

            AverageDaysInReview = d.AverageDaysInReview,

            TopStatuses = d.PipelineByStatus.Take(8).ToList(),

            RecentActivity = d.RecentActivity,

            TotalActiveCases = d.TotalActiveCases,

            CompletedCases = d.CompletedCases,

            PendingCeoApprovals = d.PendingCeoApprovals,

            WaitingPaymentCount = d.WaitingPaymentCount,

            RejectedCount = d.RejectedCount,

            CompletionRate = d.CompletionRate,

            ActivePipelineRequestedAmount = d.ActivePipelineRequestedAmount

        };

    }



    private static BoardDashboardDto BuildBoardDashboard(ExecutiveDashboardViewDto d)

    {

        var modules = d.Modules;

        var monthlyTrend = modules

            .SelectMany(m => m.MonthlyTrend)

            .GroupBy(x => new { x.Year, x.Month })

            .Select(g => new MonthlyCountDto(g.Key.Year, g.Key.Month, g.Sum(x => x.Count)))

            .OrderBy(x => x.Year).ThenBy(x => x.Month)

            .ToList();



        return new BoardDashboardDto

        {

            Modules = modules,

            CountsByStatus = d.PipelineByStatus,

            CountsByPhase = d.CountsByPhase,

            MonthlyTrend = monthlyTrend,

            CompletionRate = d.CompletionRate,

            TotalCases = d.TotalCases,

            TotalActiveVolume = modules.Sum(m => m.ActiveVolume)

        };

    }



    private async Task<ExecutiveStatsCachePayload?> LoadExecutiveCacheAsync(CancellationToken ct)

    {

        var snapshot = await statsRepository.GetByKeyAsync(DashboardRoleResolver.ExecutiveSnapshotKey, ct);

        if (snapshot is null)

            return null;



        return JsonSerializer.Deserialize<ExecutiveStatsCachePayload>(snapshot.PayloadJson, JsonOptions);

    }



    private async Task<IReadOnlyList<InboxQuickLinkDto>> LoadInboxQuickLinksAsync(string departmentKey, CancellationToken ct)

    {

        var kanban = await kanbanAppService.GetActionRequiredAsync(ct);

        if (kanban.IsFailure || kanban.Value is null)

            return [];



        return kanban.Value

            .Where(c => DashboardDepartmentModuleFilter.IsModuleVisible(departmentKey, c.Module.ToString()))

            .Take(8)

            .Select(c => new InboxQuickLinkDto(

                c.Id,

                c.CaseNumber,

                c.Module.ToString(),

                GetModuleTitle(c.Module),

                c.StatusTitle,

                c.UpdatedAt ?? c.CreatedAt))

            .ToList();

    }



    private static bool IsStale(DateTimeOffset? computedAt)

        => computedAt is null || DateTimeOffset.UtcNow - computedAt.Value > StaleThreshold;



    private static string GetModuleTitle(CaseModuleType module) => module switch

    {

        CaseModuleType.Investment => "سرمایه‌گذاری",

        CaseModuleType.Guarantee => "ضمانت‌نامه",

        CaseModuleType.GuaranteeRenewal => "تمدید ضمانت",

        CaseModuleType.Loan => "تسهیلات",

        _ => module.ToString()

    };



    private async Task<FundCreditLimitDashboardSectionDto?> LoadFundCreditLimitsSectionAsync(CancellationToken ct)

    {

        if (!FundCreditLimitAuthorization.CanAccessFundCreditLimits(userContext.Roles))

            return null;



        var section = await fundCreditLimitAppService.GetDashboardSectionAsync(ct);

        return section.IsSuccess ? section.Value : null;

    }



    private async Task<ApplicantOverviewSummaryDto> BuildApplicantOverviewSummaryAsync(CancellationToken ct)

    {

        var snapshots = await statsRepository.ListByTypeAsync(DashboardSnapshotType.Applicant, ct);

        var moduleCounts = new Dictionary<string, (string Title, int Count)>(StringComparer.OrdinalIgnoreCase);

        var applicantCount = 0;

        var activeCasesCount = 0;

        var totalDebt = 0m;

        var unpaidInstallments = 0;



        foreach (var snapshot in snapshots)

        {

            var cached = JsonSerializer.Deserialize<ApplicantStatsCachePayload>(snapshot.PayloadJson, JsonOptions);

            if (cached is null)

                continue;



            applicantCount++;

            activeCasesCount += cached.ActiveCases.Count;

            totalDebt += cached.TotalRemainingDebt;

            unpaidInstallments += cached.UnpaidInstallmentsCount;



            foreach (var group in cached.ActiveCases.GroupBy(c => c.Module))

            {

                var module = group.Key;

                var title = group.First().ModuleTitle;

                if (!moduleCounts.TryGetValue(module, out var existing))

                    moduleCounts[module] = (title, group.Count());

                else

                    moduleCounts[module] = (existing.Title, existing.Count + group.Count());

            }

        }



        return new ApplicantOverviewSummaryDto

        {

            ApplicantCount = applicantCount,

            ActiveCasesCount = activeCasesCount,

            TotalRemainingDebt = totalDebt,

            UnpaidInstallmentsCount = unpaidInstallments,

            ActiveCasesByModule = moduleCounts

                .Select(kvp => new ModuleQueueCountDto(kvp.Key, kvp.Value.Title, kvp.Value.Count))

                .OrderByDescending(x => x.Count)

                .ToList()

        };

    }

    #endregion

}
