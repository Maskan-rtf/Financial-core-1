using Core.Application.Abstractions;
using Core.Application.Kanban;
using Core.Domain.Enums;
using Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.Application.Dashboard;

public sealed class DashboardModuleAggregators(ICoreDbContext dbContext)
{
    #region Configuration

    private static readonly int[] InvestmentTerminal =
    [
        (int)CaseStatus.Completed, (int)CaseStatus.Rejected, (int)CaseStatus.Cancelled, (int)CaseStatus.Archived
    ];

    private static readonly int[] GuaranteeTerminal =
    [
        (int)GuaranteeCaseStatus.Completed, (int)GuaranteeCaseStatus.Rejected,
        (int)GuaranteeCaseStatus.Cancelled, (int)GuaranteeCaseStatus.Archived
    ];

    private static readonly int[] LoanTerminal =
    [
        (int)LoanCaseStatus.Completed, (int)LoanCaseStatus.Archived, (int)LoanCaseStatus.CanceledByCeo
    ];

    private static readonly int[] LoanPipelineTerminal =
    [
        (int)LoanCaseStatus.Completed,
        (int)LoanCaseStatus.Archived,
        (int)LoanCaseStatus.CanceledByCeo,
        (int)LoanCaseStatus.RepaymentPhase
    ];

    #endregion

    #region Public API

    public async Task<IReadOnlyList<ModuleDashboardMetricsDto>> AggregateAllModulesAsync(
        DateTimeOffset computedAt,
        string? queueDepartmentKey,
        CancellationToken ct)
    {
        var sixMonthsAgo = computedAt.AddMonths(-6);
        var modules = new List<ModuleDashboardMetricsDto>(3);

        if (DashboardDepartmentModuleFilter.IsModuleVisible(queueDepartmentKey, "Investment"))
            modules.Add(await AggregateInvestmentAsync(sixMonthsAgo, queueDepartmentKey, ct));

        if (DashboardDepartmentModuleFilter.IsModuleVisible(queueDepartmentKey, "Guarantee"))
            modules.Add(await AggregateGuaranteeAsync(sixMonthsAgo, queueDepartmentKey, ct));

        if (DashboardDepartmentModuleFilter.IsModuleVisible(queueDepartmentKey, "Loan"))
            modules.Add(await AggregateLoanAsync(sixMonthsAgo, queueDepartmentKey, ct));

        return modules;
    }

    public async Task<SystemHealthDto> AggregateSystemHealthAsync(DateTimeOffset computedAt, CancellationToken ct)
    {
        var onlineThreshold = computedAt.AddMinutes(-30);
        var dayStart = computedAt.UtcDateTime.Date;

        var onlineUsers = await dbContext.UserSessions.AsNoTracking()
            .CountAsync(s => s.RevokedAt == null && s.LastActivityAt >= onlineThreshold.UtcDateTime, ct);

        var activeSessions = await dbContext.UserSessions.AsNoTracking()
            .CountAsync(s => s.RevokedAt == null, ct);

        var dailyActiveUsers = await dbContext.UserSessions.AsNoTracking()
            .Where(s => s.LastActivityAt >= dayStart)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(ct);

        return new SystemHealthDto
        {
            OnlineUsersCount = onlineUsers,
            DailyActiveUsers = dailyActiveUsers,
            ActiveSessionsCount = activeSessions
        };
    }

    public async Task<DepartmentSpecificMetricsDto> AggregateDepartmentSpecificMetricsAsync(
        string departmentKey,
        DateTimeOffset computedAt,
        CancellationToken ct)
    {
        var since = computedAt.AddMonths(-6);
        var today = DateOnly.FromDateTime(computedAt.UtcDateTime);

        if (departmentKey.Equals("Financial", StringComparison.OrdinalIgnoreCase))
        {
            var commissions = await dbContext.GuaranteeApprovalForms.AsNoTracking()
                .Where(f => !f.Case.IsDeleted && f.CommissionAmount != null)
                .SumAsync(f => (decimal?)f.CommissionAmount, ct) ?? 0m;

            var repayments = await dbContext.LoanPayments.AsNoTracking()
                .Where(p => !p.Case.IsDeleted)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

            var overdue = await dbContext.LoanInstallments.AsNoTracking()
                .Where(i => !i.IsPaid && !i.Case.IsDeleted && i.InstallmentDate < today)
                .Select(i => new { i.TotalAmount })
                .ToListAsync(ct);

            var pendingFinancial = await dbContext.InvestmentCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && c.CurrentStatus == CaseStatus.FinancialWorksheetReview, ct);

            pendingFinancial += await dbContext.GuaranteeCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && c.CurrentStatus == GuaranteeCaseStatus.FinancialAttachmentReview, ct);

            pendingFinancial += await dbContext.LoanCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted &&
                    (c.CurrentStatus == LoanCaseStatus.PendingFinancialReview ||
                     c.CurrentStatus == LoanCaseStatus.RevisionRequestedByFinancial), ct);

            return new DepartmentSpecificMetricsDto
            {
                TotalCommissions = commissions,
                TotalRepayments = repayments,
                OverdueInstallmentsCount = overdue.Count,
                OverdueAmount = overdue.Sum(i => i.TotalAmount),
                PendingFinancialReviews = pendingFinancial
            };
        }

        if (departmentKey.Equals("Legal", StringComparison.OrdinalIgnoreCase))
        {
            var legalInvestment = await dbContext.InvestmentCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && c.CurrentPhase == CasePhase.Legal, ct);

            var legalGuarantee = await dbContext.GuaranteeCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && c.CurrentPhase == GuaranteeCasePhase.Legal, ct);

            var legalLoan = await dbContext.LoanCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted &&
                    (c.CurrentStatus == LoanCaseStatus.PendingLegalRawContract ||
                     c.CurrentStatus == LoanCaseStatus.PendingLegalFinalReview ||
                     c.CurrentStatus == LoanCaseStatus.PendingLegalFinalContract), ct);

            var pendingContracts = await dbContext.InvestmentCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted &&
                    (c.CurrentStatus == CaseStatus.WaitingSignedContractUpload ||
                     c.CurrentStatus == CaseStatus.ContractDrafting), ct);

            pendingContracts += await dbContext.GuaranteeCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted &&
                    (c.CurrentStatus == GuaranteeCaseStatus.WaitingDraftContract ||
                     c.CurrentStatus == GuaranteeCaseStatus.WaitingFinalContract), ct);

            var pendingSigned = await dbContext.GuaranteeCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted &&
                    c.CurrentStatus == GuaranteeCaseStatus.WaitingSignedContractAndAttachments, ct);

            pendingSigned += await dbContext.LoanCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && c.CurrentStatus == LoanCaseStatus.PendingApplicantSignature, ct);

            return new DepartmentSpecificMetricsDto
            {
                ContractsPendingReview = pendingContracts,
                CasesInLegalPhase = legalInvestment + legalGuarantee + legalLoan,
                PendingSignedContractUploads = pendingSigned
            };
        }

        if (departmentKey.Equals("Credit", StringComparison.OrdinalIgnoreCase))
        {
            var pendingCredit = await dbContext.GuaranteeCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted &&
                    (c.CurrentStatus == GuaranteeCaseStatus.CreditReview ||
                     c.CurrentStatus == GuaranteeCaseStatus.ApprovalFormEntry), ct);

            pendingCredit += await dbContext.LoanCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && c.CurrentStatus == LoanCaseStatus.PendingCreditReview, ct);

            var revisionCount = await dbContext.LoanCaseWorkflowHistories.AsNoTracking()
                .CountAsync(h => h.CreatedAt >= since &&
                    (h.ToStatus == LoanCaseStatus.RevisionRequestedByCredit || h.Action.Contains("Revision")), ct);

            revisionCount += await dbContext.GuaranteeCaseWorkflowHistories.AsNoTracking()
                .CountAsync(h => h.CreatedAt >= since && h.Action.Contains("Revision"), ct);

            return new DepartmentSpecificMetricsDto
            {
                PendingCreditReviews = pendingCredit,
                RevisionCountLast6Months = revisionCount
            };
        }

        if (departmentKey.Equals("Investment", StringComparison.OrdinalIgnoreCase))
        {
            var pendingValuations = await dbContext.InvestmentCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted &&
                    (c.CurrentStatus == CaseStatus.InitialValuation ||
                     c.CurrentStatus == CaseStatus.SecondaryValuation), ct);

            var waitingPayment = await dbContext.InvestmentCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && c.CurrentStatus == CaseStatus.WaitingPayment, ct);

            return new DepartmentSpecificMetricsDto
            {
                PendingValuations = pendingValuations,
                WaitingPaymentCount = waitingPayment
            };
        }

        if (departmentKey.Equals("Technical", StringComparison.OrdinalIgnoreCase))
        {
            var activePools = await dbContext.FundCreditLimits.AsNoTracking()
                .CountAsync(f => f.ExpiresAt >= today, ct);

            return new DepartmentSpecificMetricsDto
            {
                TechnicalReviewQueue = 0,
                ActiveFundCreditPools = activePools
            };
        }

        return new DepartmentSpecificMetricsDto();
    }

    public async Task<int> CountDepartmentQueueAsync(string departmentKey, string module, CancellationToken ct)
    {
        if (module.Equals("Investment", StringComparison.OrdinalIgnoreCase))
        {
            var statuses = DashboardKanbanStatusFilters.GetInvestmentQueueStatuses(departmentKey);
            if (statuses.Count == 0) return 0;
            return await dbContext.InvestmentCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && statuses.Contains((int)c.CurrentStatus), ct);
        }

        if (module.Equals("Guarantee", StringComparison.OrdinalIgnoreCase))
        {
            var statuses = DashboardKanbanStatusFilters.GetGuaranteeQueueStatuses(departmentKey);
            if (statuses.Count == 0) return 0;
            return await dbContext.GuaranteeCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && statuses.Contains((int)c.CurrentStatus), ct);
        }

        if (module.Equals("Loan", StringComparison.OrdinalIgnoreCase))
        {
            var statuses = DashboardKanbanStatusFilters.GetLoanQueueStatuses(departmentKey);
            if (statuses.Count == 0) return 0;
            return await dbContext.LoanCases.AsNoTracking()
                .CountAsync(c => !c.IsDeleted && statuses.Contains((int)c.CurrentStatus), ct);
        }

        if (module.Equals("GuaranteeRenewal", StringComparison.OrdinalIgnoreCase))
            return 0;

        return 0;
    }

    #endregion

    #region Private

    private async Task<ModuleDashboardMetricsDto> AggregateInvestmentAsync(
        DateTimeOffset sixMonthsAgo,
        string? queueDepartmentKey,
        CancellationToken ct)
    {
        var totalCases = await dbContext.InvestmentCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted, ct);

        var completedCases = await dbContext.InvestmentCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == CaseStatus.Completed, ct);

        var activeCases = await dbContext.InvestmentCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && !InvestmentTerminal.Contains((int)c.CurrentStatus), ct);

        var activeVolume = await dbContext.InvestmentCaseApplicantProfiles.AsNoTracking()
            .Where(d => !d.Case.IsDeleted && !InvestmentTerminal.Contains((int)d.Case.CurrentStatus))
            .SumAsync(d => (decimal?)d.RequestedAmount, ct) ?? 0m;

        var pendingCeo = await dbContext.InvestmentCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == CaseStatus.WaitingCeoApproval, ct);

        var rejected = await dbContext.InvestmentCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == CaseStatus.Rejected, ct);

        var cancelled = await dbContext.InvestmentCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == CaseStatus.Cancelled, ct);

        var archived = await dbContext.InvestmentCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == CaseStatus.Archived, ct);

        var pipelineRaw = await dbContext.InvestmentCases.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.CurrentStatus)
            .Select(g => new { Status = (int)g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var pipelineByStatus = pipelineRaw
            .Select(x => new StatusCountDto(x.Status, CaseKanbanRules.GetStatusTitle((CaseStatus)x.Status), x.Count))
            .OrderByDescending(x => x.Count)
            .ToList();

        var monthlyTrendRaw = await dbContext.InvestmentCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CreatedAt >= sixMonthsAgo)
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        var monthlyTrend = monthlyTrendRaw
            .Select(x => new MonthlyCountDto(x.Year, x.Month, x.Count))
            .ToList();

        var monthlyFinancialRaw = await dbContext.PaymentRecords.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && !p.Case.IsDeleted && p.CreatedAt >= sixMonthsAgo)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount), CaseCount = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        var monthlyFinancial = monthlyFinancialRaw
            .Select(x => new MonthlyFinancialOutputDto(x.Year, x.Month, x.Amount, x.CaseCount))
            .ToList();

        var recentActivity = await dbContext.CaseWorkflowHistories.AsNoTracking()
            .Where(h => !h.Case.IsDeleted)
            .OrderByDescending(h => h.CreatedAt)
            .Take(10)
            .Select(h => new RecentActivityDto(
                h.CaseId, h.Case.CaseNumber, (int)h.FromStatus, (int)h.ToStatus, h.Action, h.CreatedAt))
            .ToListAsync(ct);

        var avgReviewDays = await ComputeInvestmentAvgReviewDaysAsync(ct);

        var queueCount = queueDepartmentKey is null
            ? 0
            : await CountDepartmentQueueAsync(queueDepartmentKey, "Investment", ct);

        var completionRate = totalCases == 0 ? 0 : Math.Round(completedCases * 100.0 / totalCases, 1);

        return new ModuleDashboardMetricsDto
        {
            Module = "Investment",
            ModuleTitle = "سرمایه‌گذاری",
            ActiveVolume = activeVolume,
            TotalCases = totalCases,
            ActiveCases = activeCases,
            CompletedCases = completedCases,
            PendingCeoApprovals = pendingCeo,
            RejectedCount = rejected,
            CancelledCount = cancelled,
            ArchivedCount = archived,
            CompletionRate = completionRate,
            AverageDaysInReview = avgReviewDays,
            QueueCount = queueCount,
            PipelineByStatus = pipelineByStatus,
            MonthlyTrend = monthlyTrend,
            MonthlyFinancialOutput = monthlyFinancial,
            RecentActivity = recentActivity
        };
    }

    private async Task<ModuleDashboardMetricsDto> AggregateGuaranteeAsync(
        DateTimeOffset sixMonthsAgo,
        string? queueDepartmentKey,
        CancellationToken ct)
    {
        var totalCases = await dbContext.GuaranteeCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted, ct);

        var completedCases = await dbContext.GuaranteeCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == GuaranteeCaseStatus.Completed, ct);

        var activeCases = await dbContext.GuaranteeCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && !GuaranteeTerminal.Contains((int)c.CurrentStatus), ct);

        var activeVolume = await dbContext.GuaranteeCaseApplications.AsNoTracking()
            .Where(a => !a.Case.IsDeleted && !GuaranteeTerminal.Contains((int)a.Case.CurrentStatus))
            .SumAsync(a => (decimal?)a.RequestedGuaranteeAmount, ct) ?? 0m;

        var pendingCeo = await dbContext.GuaranteeCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted &&
                (c.CurrentStatus == GuaranteeCaseStatus.CeoApprovalInitial ||
                 c.CurrentStatus == GuaranteeCaseStatus.CeoApprovalFinal ||
                 c.CurrentStatus == GuaranteeCaseStatus.AmendmentCeoApproval), ct);

        var rejected = await dbContext.GuaranteeCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == GuaranteeCaseStatus.Rejected, ct);

        var cancelled = await dbContext.GuaranteeCases.AsNoTracking()
            .CountAsync(
                c => !c.IsDeleted
                    && (c.CurrentStatus == GuaranteeCaseStatus.Cancelled
                        || c.CurrentStatus == GuaranteeCaseStatus.AmendmentCompleted),
                ct);

        var archived = await dbContext.GuaranteeCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == GuaranteeCaseStatus.Archived, ct);

        var pipelineRaw = await dbContext.GuaranteeCases.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.CurrentStatus)
            .Select(g => new { Status = (int)g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var pipelineByStatus = pipelineRaw
            .Select(x => new StatusCountDto(
                x.Status, GuaranteeKanbanRules.GetStatusTitle((GuaranteeCaseStatus)x.Status), x.Count))
            .OrderByDescending(x => x.Count)
            .ToList();

        var monthlyTrendRaw = await dbContext.GuaranteeCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CreatedAt >= sixMonthsAgo)
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        var monthlyTrend = monthlyTrendRaw
            .Select(x => new MonthlyCountDto(x.Year, x.Month, x.Count))
            .ToList();

        var monthlyFinancialRaw = await dbContext.GuaranteeApprovalForms.AsNoTracking()
            .Where(f => !f.Case.IsDeleted && f.CommissionAmount != null && f.Case.CreatedAt >= sixMonthsAgo)
            .GroupBy(f => new { f.Case.CreatedAt.Year, f.Case.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.CommissionAmount ?? 0m), CaseCount = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        var monthlyFinancial = monthlyFinancialRaw
            .Select(x => new MonthlyFinancialOutputDto(x.Year, x.Month, x.Amount, x.CaseCount))
            .ToList();

        var recentActivity = await dbContext.GuaranteeCaseWorkflowHistories.AsNoTracking()
            .Where(h => !h.Case.IsDeleted)
            .OrderByDescending(h => h.CreatedAt)
            .Take(10)
            .Select(h => new RecentActivityDto(
                h.CaseId, h.Case.CaseNumber, (int)h.FromStatus, (int)h.ToStatus, h.Action, h.CreatedAt))
            .ToListAsync(ct);

        var queueCount = queueDepartmentKey is null
            ? 0
            : await CountDepartmentQueueAsync(queueDepartmentKey, "Guarantee", ct);

        var completionRate = totalCases == 0 ? 0 : Math.Round(completedCases * 100.0 / totalCases, 1);

        return new ModuleDashboardMetricsDto
        {
            Module = "Guarantee",
            ModuleTitle = "ضمانت‌نامه",
            ActiveVolume = activeVolume,
            TotalCases = totalCases,
            ActiveCases = activeCases,
            CompletedCases = completedCases,
            PendingCeoApprovals = pendingCeo,
            RejectedCount = rejected,
            CancelledCount = cancelled,
            ArchivedCount = archived,
            CompletionRate = completionRate,
            QueueCount = queueCount,
            PipelineByStatus = pipelineByStatus,
            MonthlyTrend = monthlyTrend,
            MonthlyFinancialOutput = monthlyFinancial,
            RecentActivity = recentActivity
        };
    }

    private async Task<ModuleDashboardMetricsDto> AggregateLoanAsync(
        DateTimeOffset sixMonthsAgo,
        string? queueDepartmentKey,
        CancellationToken ct)
    {
        var totalCases = await dbContext.LoanCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted, ct);

        var completedCases = await dbContext.LoanCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == LoanCaseStatus.Completed, ct);

        var activeCases = await dbContext.LoanCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && !LoanPipelineTerminal.Contains((int)c.CurrentStatus), ct);

        var activeVolume = await dbContext.LoanCaseApplications.AsNoTracking()
            .Where(a => !a.Case.IsDeleted && !LoanPipelineTerminal.Contains((int)a.Case.CurrentStatus))
            .SumAsync(a => (decimal?)a.RequestedAmount, ct) ?? 0m;

        var pendingCeo = await dbContext.LoanCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted &&
                (c.CurrentStatus == LoanCaseStatus.PendingCeoInitialApproval ||
                 c.CurrentStatus == LoanCaseStatus.PendingCeoFinalApproval), ct);

        var rejected = await dbContext.LoanCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == LoanCaseStatus.CanceledByCeo, ct);

        var cancelled = 0;
        var archived = await dbContext.LoanCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CurrentStatus == LoanCaseStatus.Archived, ct);

        var pipelineRaw = await dbContext.LoanCases.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.CurrentStatus)
            .Select(g => new { Status = (int)g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var pipelineByStatus = pipelineRaw
            .Select(x => new StatusCountDto(
                x.Status, LoanKanbanRules.GetStatusTitle((LoanCaseStatus)x.Status), x.Count))
            .OrderByDescending(x => x.Count)
            .ToList();

        var monthlyTrendRaw = await dbContext.LoanCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CreatedAt >= sixMonthsAgo)
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        var monthlyTrend = monthlyTrendRaw
            .Select(x => new MonthlyCountDto(x.Year, x.Month, x.Count))
            .ToList();

        var monthlyFinancialRaw = await dbContext.LoanPayments.AsNoTracking()
            .Where(p => !p.Case.IsDeleted && p.CreatedAt >= sixMonthsAgo)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount), CaseCount = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        var monthlyFinancial = monthlyFinancialRaw
            .Select(x => new MonthlyFinancialOutputDto(x.Year, x.Month, x.Amount, x.CaseCount))
            .ToList();

        var recentActivity = await dbContext.LoanCaseWorkflowHistories.AsNoTracking()
            .Where(h => !h.Case.IsDeleted)
            .OrderByDescending(h => h.CreatedAt)
            .Take(10)
            .Select(h => new RecentActivityDto(
                h.CaseId, h.Case.CaseNumber, (int)h.FromStatus, (int)h.ToStatus, h.Action, h.CreatedAt))
            .ToListAsync(ct);

        var queueCount = queueDepartmentKey is null
            ? 0
            : await CountDepartmentQueueAsync(queueDepartmentKey, "Loan", ct);

        var completionRate = totalCases == 0 ? 0 : Math.Round(completedCases * 100.0 / totalCases, 1);

        return new ModuleDashboardMetricsDto
        {
            Module = "Loan",
            ModuleTitle = "تسهیلات",
            ActiveVolume = activeVolume,
            TotalCases = totalCases,
            ActiveCases = activeCases,
            CompletedCases = completedCases,
            PendingCeoApprovals = pendingCeo,
            RejectedCount = rejected,
            CancelledCount = cancelled,
            ArchivedCount = archived,
            CompletionRate = completionRate,
            QueueCount = queueCount,
            PipelineByStatus = pipelineByStatus,
            MonthlyTrend = monthlyTrend,
            MonthlyFinancialOutput = monthlyFinancial,
            RecentActivity = recentActivity
        };
    }

    public async Task<IReadOnlyList<StatusCountDto>> AggregateCrossModulePhaseCountsAsync(CancellationToken ct)
    {
        var investmentPhases = await dbContext.InvestmentCases.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.CurrentPhase)
            .Select(g => new { Phase = (int)g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var guaranteePhases = await dbContext.GuaranteeCases.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.CurrentPhase)
            .Select(g => new { Phase = (int)g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var loanPhases = await dbContext.LoanCases.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.CurrentPhase)
            .Select(g => new { Phase = (int)g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var combined = new List<StatusCountDto>();
        combined.AddRange(investmentPhases.Select(x => new StatusCountDto(
            x.Phase,
            "سرمایه‌گذاری — " + CaseKanbanRules.GetPhaseTitle((CasePhase)x.Phase),
            x.Count)));
        combined.AddRange(guaranteePhases.Select(x => new StatusCountDto(
            100 + x.Phase,
            "ضمانت — " + GuaranteeKanbanRules.GetPhaseTitle((GuaranteeCasePhase)x.Phase),
            x.Count)));
        combined.AddRange(loanPhases.Select(x => new StatusCountDto(
            200 + x.Phase,
            "تسهیلات — " + LoanKanbanRules.GetPhaseTitle((LoanCasePhase)x.Phase),
            x.Count)));

        return combined.OrderByDescending(x => x.Count).ToList();
    }

    private async Task<double> ComputeInvestmentAvgReviewDaysAsync(CancellationToken ct)
    {
        var reviewStatuses = new[]
        {
            (int)CaseStatus.ReviewDataEntry1, (int)CaseStatus.ReviewDataEntry2,
            (int)CaseStatus.InitialValuation, (int)CaseStatus.SecondaryValuation,
            (int)CaseStatus.FinancialWorksheetReview, (int)CaseStatus.WaitingCeoApproval
        };

        var samples = await dbContext.InvestmentCases.AsNoTracking()
            .Where(c => !c.IsDeleted && reviewStatuses.Contains((int)c.CurrentStatus) && c.UpdatedAt != null)
            .OrderBy(c => c.UpdatedAt)
            .Select(c => new { c.CreatedAt, UpdatedAt = c.UpdatedAt!.Value })
            .Take(500)
            .ToListAsync(ct);

        return samples.Count == 0
            ? 0
            : Math.Round(samples.Average(s => (s.UpdatedAt - s.CreatedAt).TotalDays), 1);
    }

    #endregion
}
