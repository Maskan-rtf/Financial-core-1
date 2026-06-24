using System.Text.Json;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.Kanban;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Dashboard;

public sealed class DashboardAggregationService(
    ICoreDbContext dbContext,
    IDashboardStatsRepository statsRepository,
    DashboardModuleAggregators moduleAggregators,
    ILogger<DashboardAggregationService> logger) : IDashboardAggregationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly int[] InvestmentTerminal =
    [
        (int)CaseStatus.Completed, (int)CaseStatus.Rejected, (int)CaseStatus.Cancelled, (int)CaseStatus.Archived
    ];

    private static readonly int[] GuaranteeTerminal =
    [
        (int)GuaranteeCaseStatus.Completed, (int)GuaranteeCaseStatus.AmendmentCompleted, (int)GuaranteeCaseStatus.Rejected,
        (int)GuaranteeCaseStatus.Cancelled, (int)GuaranteeCaseStatus.Archived
    ];

    private static readonly int[] LoanTerminal =
    [
        (int)LoanCaseStatus.Completed, (int)LoanCaseStatus.Archived, (int)LoanCaseStatus.CanceledByCeo
    ];

    public async Task AggregateAllAsync(CancellationToken cancellationToken = default)
    {
        var computedAt = DateTimeOffset.UtcNow;
        logger.LogInformation("Dashboard aggregation started at {ComputedAt}", computedAt);

        await UpsertExecutiveAsync(computedAt, cancellationToken);

        foreach (var departmentKey in DashboardRoleResolver.DepartmentKeys)
            await UpsertDepartmentAsync(departmentKey, computedAt, cancellationToken);

        await UpsertApplicantSnapshotsAsync(computedAt, cancellationToken);

        logger.LogInformation("Dashboard aggregation completed at {ComputedAt}", computedAt);
    }

    private async Task UpsertExecutiveAsync(DateTimeOffset computedAt, CancellationToken ct)
    {
        var monthStart = new DateTimeOffset(computedAt.Year, computedAt.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var modules = await moduleAggregators.AggregateAllModulesAsync(computedAt, queueDepartmentKey: null, ct);
        var systemHealth = await moduleAggregators.AggregateSystemHealthAsync(computedAt, ct);
        var bottlenecks = await BuildDepartmentBottlenecksAsync(ct);

        var investment = modules.First(m => m.Module == "Investment");
        var guarantee = modules.First(m => m.Module == "Guarantee");
        var loan = modules.First(m => m.Module == "Loan");

        var pendingCount = investment.ActiveCases + guarantee.ActiveCases + loan.ActiveCases;
        var completedCount = investment.CompletedCases + guarantee.CompletedCases + loan.CompletedCases;
        var rejectedCount = investment.RejectedCount + guarantee.RejectedCount + loan.RejectedCount;
        var cancelledCount = investment.CancelledCount + guarantee.CancelledCount + loan.CancelledCount;
        var archivedCount = investment.ArchivedCount + guarantee.ArchivedCount + loan.ArchivedCount;
        var totalCases = investment.TotalCases + guarantee.TotalCases + loan.TotalCases;
        var completionRate = totalCases == 0 ? 0 : Math.Round(completedCount * 100.0 / totalCases, 1);

        var statusDistribution = new List<StatusBucketDto>
        {
            new("InProgress", "پرونده‌های جاری", pendingCount),
            new("Completed", "تکمیل‌شده", completedCount),
            new("Rejected", "رد شده", rejectedCount),
            new("Cancelled", "لغو شده", cancelledCount),
            new("Archived", "بایگانی", archivedCount)
        };

        var monthlyFinancial = modules
            .SelectMany(m => m.MonthlyFinancialOutput)
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new MonthlyFinancialOutputDto(
                g.Key.Year, g.Key.Month, g.Sum(x => x.Amount), g.Sum(x => x.CaseCount)))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        var pipelineByStatus = modules
            .SelectMany(m => m.PipelineByStatus)
            .GroupBy(x => x.Status)
            .Select(g => new StatusCountDto(g.Key, g.First().StatusTitle, g.Sum(x => x.Count)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var countsByPhase = await moduleAggregators.AggregateCrossModulePhaseCountsAsync(ct);

        var monthlyTrend = modules
            .SelectMany(m => m.MonthlyTrend)
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new MonthlyCountDto(g.Key.Year, g.Key.Month, g.Sum(x => x.Count)))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        var recentActivity = modules
            .SelectMany(m => m.RecentActivity)
            .OrderByDescending(a => a.CreatedAt)
            .Take(15)
            .ToList();

        var pendingCeo = investment.PendingCeoApprovals + guarantee.PendingCeoApprovals + loan.PendingCeoApprovals;
        var waitingPayment = investment.PipelineByStatus
            .FirstOrDefault(x => x.Status == (int)CaseStatus.WaitingPayment)?.Count ?? 0;

        var casesThisMonth = await dbContext.InvestmentCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CreatedAt >= monthStart, ct);
        casesThisMonth += await dbContext.GuaranteeCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CreatedAt >= monthStart, ct);
        casesThisMonth += await dbContext.LoanCases.AsNoTracking()
            .CountAsync(c => !c.IsDeleted && c.CreatedAt >= monthStart, ct);

        var totalRequested = await dbContext.InvestmentCaseApplicantProfiles.AsNoTracking()
            .Where(d => !d.Case.IsDeleted)
            .SumAsync(d => (decimal?)d.RequestedAmount, ct) ?? 0m;
        totalRequested += await dbContext.GuaranteeCaseApplications.AsNoTracking()
            .Where(a => !a.Case.IsDeleted)
            .SumAsync(a => (decimal?)a.RequestedGuaranteeAmount, ct) ?? 0m;
        totalRequested += await dbContext.LoanCaseApplications.AsNoTracking()
            .Where(a => !a.Case.IsDeleted)
            .SumAsync(a => (decimal?)a.RequestedAmount, ct) ?? 0m;

        var approvedPayments = await dbContext.PaymentRecords.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && !p.Case.IsDeleted)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var loanPayments = await dbContext.LoanPayments.AsNoTracking()
            .Where(p => !p.Case.IsDeleted)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var activePipelineRequested = investment.ActiveVolume + guarantee.ActiveVolume + loan.ActiveVolume;

        var payload = new ExecutiveStatsCachePayload
        {
            Data = new ExecutiveDashboardViewDto
            {
                Modules = modules,
                SystemHealth = systemHealth,
                ActiveGuaranteesVolume = guarantee.ActiveVolume,
                ActiveInvestmentsVolume = investment.ActiveVolume,
                ActiveLoansVolume = loan.ActiveVolume,
                StatusDistribution = statusDistribution,
                MonthlyFinancialOutput = monthlyFinancial,
                OnlineUsersCount = systemHealth.OnlineUsersCount,
                DailyActiveUsers = systemHealth.DailyActiveUsers,
                DepartmentBottlenecks = bottlenecks,
                PipelineByStatus = pipelineByStatus,
                RecentActivity = recentActivity,
                TotalActiveCases = pendingCount,
                CompletedCases = completedCount,
                PendingCeoApprovals = pendingCeo,
                WaitingPaymentCount = waitingPayment,
                RejectedCount = rejectedCount,
                CompletionRate = completionRate,
                TotalRequestedAmount = totalRequested,
                ApprovedPaymentsSum = approvedPayments + loanPayments,
                CasesThisMonth = casesThisMonth,
                AverageDaysInReview = investment.AverageDaysInReview,
                ActivePipelineRequestedAmount = activePipelineRequested,
                CountsByPhase = countsByPhase,
                MonthlyTrend = monthlyTrend,
                TotalCases = totalCases
            }
        };

        await statsRepository.UpsertAsync(new DashboardStatsSnapshot
        {
            Id = Guid.NewGuid(),
            SnapshotKey = DashboardRoleResolver.ExecutiveSnapshotKey,
            SnapshotType = DashboardSnapshotType.Executive,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            ComputedAtUtc = computedAt
        }, ct);
    }

    private async Task UpsertDepartmentAsync(string departmentKey, DateTimeOffset computedAt, CancellationToken ct)
    {
        var title = DashboardRoleResolver.GetDepartmentTitle(departmentKey);
        var modules = await moduleAggregators.AggregateAllModulesAsync(computedAt, departmentKey, ct);
        var specificMetrics = await moduleAggregators.AggregateDepartmentSpecificMetricsAsync(departmentKey, computedAt, ct);

        var investmentCount = await moduleAggregators.CountDepartmentQueueAsync(departmentKey, "Investment", ct);
        var guaranteeCount = await moduleAggregators.CountDepartmentQueueAsync(departmentKey, "Guarantee", ct);
        var loanCount = await moduleAggregators.CountDepartmentQueueAsync(departmentKey, "Loan", ct);
        var renewalCount = await moduleAggregators.CountDepartmentQueueAsync(departmentKey, "GuaranteeRenewal", ct);

        var queueByModule = DashboardDepartmentModuleFilter.FilterQueue(
            departmentKey,
            new List<ModuleQueueCountDto>
            {
                new("Investment", "سرمایه‌گذاری", investmentCount),
                new("Guarantee", "ضمانت‌نامه", guaranteeCount),
                new("Loan", "تسهیلات", loanCount),
                new("GuaranteeRenewal", "تمدید ضمانت", renewalCount)
            });

        var totalQueue = queueByModule.Sum(x => x.Count);
        var revisionRate = await ComputeRevisionRateAsync(departmentKey, totalQueue, ct);

        var payload = new DepartmentStatsCachePayload
        {
            DepartmentKey = departmentKey,
            DepartmentTitle = title,
            TotalQueueCount = totalQueue,
            QueueByModule = queueByModule,
            Modules = modules,
            SpecificMetrics = specificMetrics,
            RevisionRatePercent = revisionRate
        };

        await statsRepository.UpsertAsync(new DashboardStatsSnapshot
        {
            Id = Guid.NewGuid(),
            SnapshotKey = DashboardRoleResolver.DepartmentSnapshotKey(departmentKey),
            SnapshotType = DashboardSnapshotType.Department,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            ComputedAtUtc = computedAt
        }, ct);
    }

    private async Task UpsertApplicantSnapshotsAsync(DateTimeOffset computedAt, CancellationToken ct)
    {
        var applicantIds = await dbContext.InvestmentCases.AsNoTracking()
            .Where(c => !c.IsDeleted && !InvestmentTerminal.Contains((int)c.CurrentStatus))
            .Select(c => c.ApplicantUserId)
            .Union(dbContext.GuaranteeCases.AsNoTracking()
                .Where(c => !c.IsDeleted && !GuaranteeTerminal.Contains((int)c.CurrentStatus))
                .Select(c => c.ApplicantUserId))
            .Union(dbContext.LoanCases.AsNoTracking()
                .Where(c => !c.IsDeleted && !LoanTerminal.Contains((int)c.CurrentStatus))
                .Select(c => c.ApplicantUserId))
            .ToListAsync(ct);

        foreach (var applicantId in applicantIds)
            await UpsertApplicantAsync(applicantId, computedAt, ct);
    }

    private async Task UpsertApplicantAsync(string applicantUserId, DateTimeOffset computedAt, CancellationToken ct)
    {
        var activeCases = new List<ApplicantCaseProgressDto>();
        var pendingActions = 0;
        const string applicantRole = UserRoleClaims.Applicant;

        var investments = await dbContext.InvestmentCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.ApplicantUserId == applicantUserId && !InvestmentTerminal.Contains((int)c.CurrentStatus))
            .Select(c => new { c.Id, c.CaseNumber, c.CurrentStatus, c.CurrentPhase, c.UpdatedAt, c.CreatedAt })
            .ToListAsync(ct);

        foreach (var c in investments)
        {
            if (CaseKanbanRules.IsActionRequired(c.CurrentStatus, applicantRole))
                pendingActions++;

            activeCases.Add(new ApplicantCaseProgressDto(
                c.Id, c.CaseNumber, "Investment", "سرمایه‌گذاری",
                (int)c.CurrentStatus, CaseKanbanRules.GetStatusTitle(c.CurrentStatus),
                CaseKanbanRules.GetPhaseTitle(c.CurrentPhase),
                ComputeProgressPercent((int)c.CurrentStatus, InvestmentTerminal, (int)CaseStatus.Completed),
                c.UpdatedAt ?? c.CreatedAt));
        }

        var guarantees = await dbContext.GuaranteeCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.ApplicantUserId == applicantUserId && !GuaranteeTerminal.Contains((int)c.CurrentStatus))
            .Select(c => new { c.Id, c.CaseNumber, c.CurrentStatus, c.CurrentPhase, c.UpdatedAt, c.CreatedAt })
            .ToListAsync(ct);

        foreach (var c in guarantees)
        {
            if (GuaranteeKanbanRules.IsActionRequired(c.CurrentStatus, applicantRole))
                pendingActions++;

            activeCases.Add(new ApplicantCaseProgressDto(
                c.Id, c.CaseNumber, "Guarantee", "ضمانت‌نامه",
                (int)c.CurrentStatus, GuaranteeKanbanRules.GetStatusTitle(c.CurrentStatus),
                GuaranteeKanbanRules.GetPhaseTitle(c.CurrentPhase),
                ComputeGuaranteeProgressPercent((int)c.CurrentStatus),
                c.UpdatedAt ?? c.CreatedAt));
        }

        var loans = await dbContext.LoanCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.ApplicantUserId == applicantUserId && !LoanTerminal.Contains((int)c.CurrentStatus))
            .Select(c => new { c.Id, c.CaseNumber, c.CurrentStatus, c.CurrentPhase, c.UpdatedAt, c.CreatedAt })
            .ToListAsync(ct);

        foreach (var c in loans)
        {
            if (LoanKanbanRules.IsActionRequired(c.CurrentStatus, applicantRole))
                pendingActions++;

            activeCases.Add(new ApplicantCaseProgressDto(
                c.Id, c.CaseNumber, "Loan", "تسهیلات",
                (int)c.CurrentStatus, LoanKanbanRules.GetStatusTitle(c.CurrentStatus),
                LoanKanbanRules.GetPhaseTitle(c.CurrentPhase),
                ComputeProgressPercent((int)c.CurrentStatus, LoanTerminal, (int)LoanCaseStatus.Completed),
                c.UpdatedAt ?? c.CreatedAt));
        }

        var unpaidInstallments = await dbContext.LoanInstallments.AsNoTracking()
            .Where(i => !i.IsPaid && !i.Case.IsDeleted && i.Case.ApplicantUserId == applicantUserId)
            .Select(i => new { i.TotalAmount })
            .ToListAsync(ct);

        var recentComments = new List<ApplicantRecentCommentDto>();

        var investmentComments = await dbContext.CaseComments.AsNoTracking()
            .Where(c => c.Case.ApplicantUserId == applicantUserId && !c.Case.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new ApplicantRecentCommentDto(
                c.CaseId, c.Case.CaseNumber, "Investment", "سرمایه‌گذاری",
                c.Message, c.SenderRole ?? "", c.CreatedAt))
            .ToListAsync(ct);
        recentComments.AddRange(investmentComments);

        var guaranteeComments = await dbContext.GuaranteeCaseComments.AsNoTracking()
            .Where(c => c.Case.ApplicantUserId == applicantUserId && !c.Case.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new ApplicantRecentCommentDto(
                c.CaseId, c.Case.CaseNumber, "Guarantee", "ضمانت‌نامه",
                c.Message, c.SenderRole ?? "", c.CreatedAt))
            .ToListAsync(ct);
        recentComments.AddRange(guaranteeComments);

        var loanComments = await dbContext.LoanCaseComments.AsNoTracking()
            .Where(c => c.Case.ApplicantUserId == applicantUserId && !c.Case.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new ApplicantRecentCommentDto(
                c.CaseId, c.Case.CaseNumber, "Loan", "تسهیلات",
                c.Message, c.SenderRole ?? "", c.CreatedAt))
            .ToListAsync(ct);
        recentComments.AddRange(loanComments);

        recentComments = recentComments.OrderByDescending(c => c.CreatedAt).Take(10).ToList();

        var payload = new ApplicantStatsCachePayload
        {
            UserId = applicantUserId,
            ActiveCases = activeCases.OrderByDescending(c => c.UpdatedAt).ToList(),
            PendingActionsCount = pendingActions,
            TotalRemainingDebt = unpaidInstallments.Sum(i => i.TotalAmount),
            UnpaidInstallmentsCount = unpaidInstallments.Count,
            RecentComments = recentComments
        };

        await statsRepository.UpsertAsync(new DashboardStatsSnapshot
        {
            Id = Guid.NewGuid(),
            SnapshotKey = DashboardRoleResolver.ApplicantSnapshotKey(applicantUserId),
            SnapshotType = DashboardSnapshotType.Applicant,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            ComputedAtUtc = computedAt
        }, ct);
    }

    private async Task<IReadOnlyList<DepartmentBottleneckDto>> BuildDepartmentBottlenecksAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var result = new List<DepartmentBottleneckDto>();

        var legalInvestment = await dbContext.InvestmentCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CurrentPhase == CasePhase.Legal && c.UpdatedAt != null)
            .Select(c => (now - c.UpdatedAt!.Value).TotalDays)
            .ToListAsync(ct);

        var legalGuarantee = await dbContext.GuaranteeCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CurrentPhase == GuaranteeCasePhase.Legal && c.UpdatedAt != null)
            .Select(c => (now - c.UpdatedAt!.Value).TotalDays)
            .ToListAsync(ct);

        var legalLoan = await dbContext.LoanCases.AsNoTracking()
            .Where(c => !c.IsDeleted &&
                        (c.CurrentStatus == LoanCaseStatus.PendingLegalRawContract ||
                         c.CurrentStatus == LoanCaseStatus.PendingLegalFinalReview ||
                         c.CurrentStatus == LoanCaseStatus.PendingLegalFinalContract) &&
                        c.UpdatedAt != null)
            .Select(c => (now - c.UpdatedAt!.Value).TotalDays)
            .ToListAsync(ct);

        var legalSamples = legalInvestment.Concat(legalGuarantee).Concat(legalLoan).ToList();
        result.Add(new DepartmentBottleneckDto(
            "Legal", "حقوقی",
            legalSamples.Count == 0 ? 0 : Math.Round(legalSamples.Average(), 1),
            legalSamples.Count));

        var financeInvestment = await dbContext.InvestmentCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CurrentPhase == CasePhase.Finance && c.UpdatedAt != null)
            .Select(c => (now - c.UpdatedAt!.Value).TotalDays)
            .ToListAsync(ct);

        var financeGuarantee = await dbContext.GuaranteeCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CurrentPhase == GuaranteeCasePhase.Finance && c.UpdatedAt != null)
            .Select(c => (now - c.UpdatedAt!.Value).TotalDays)
            .ToListAsync(ct);

        var financeLoan = await dbContext.LoanCases.AsNoTracking()
            .Where(c => !c.IsDeleted &&
                        (c.CurrentStatus == LoanCaseStatus.PendingFinancialReview ||
                         c.CurrentStatus == LoanCaseStatus.RevisionRequestedByFinancial) &&
                        c.UpdatedAt != null)
            .Select(c => (now - c.UpdatedAt!.Value).TotalDays)
            .ToListAsync(ct);

        var financeSamples = financeInvestment.Concat(financeGuarantee).Concat(financeLoan).ToList();
        result.Add(new DepartmentBottleneckDto(
            "Financial", "مالی",
            financeSamples.Count == 0 ? 0 : Math.Round(financeSamples.Average(), 1),
            financeSamples.Count));

        var creditGuarantee = await dbContext.GuaranteeCases.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CurrentPhase == GuaranteeCasePhase.CreditAssessment && c.UpdatedAt != null)
            .Select(c => (now - c.UpdatedAt!.Value).TotalDays)
            .ToListAsync(ct);

        var creditLoan = await dbContext.LoanCases.AsNoTracking()
            .Where(c => !c.IsDeleted &&
                        (c.CurrentStatus == LoanCaseStatus.PendingCreditReview ||
                         c.CurrentStatus == LoanCaseStatus.RevisionRequestedByCredit) &&
                        c.UpdatedAt != null)
            .Select(c => (now - c.UpdatedAt!.Value).TotalDays)
            .ToListAsync(ct);

        var creditSamples = creditGuarantee.Concat(creditLoan).ToList();
        result.Add(new DepartmentBottleneckDto(
            "Credit", "اعتبارات",
            creditSamples.Count == 0 ? 0 : Math.Round(creditSamples.Average(), 1),
            creditSamples.Count));

        return result;
    }

    private async Task<double> ComputeRevisionRateAsync(string departmentKey, int queueCount, CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddMonths(-6);
        var revisionCount = 0;

        if (departmentKey.Equals("Investment", StringComparison.OrdinalIgnoreCase))
        {
            revisionCount = await dbContext.CaseRevisions.AsNoTracking()
                .CountAsync(r => r.CreatedAt >= since, ct);
        }
        else if (departmentKey.Equals("Credit", StringComparison.OrdinalIgnoreCase))
        {
            revisionCount = await dbContext.LoanCaseWorkflowHistories.AsNoTracking()
                .CountAsync(h => h.CreatedAt >= since &&
                                 (h.ToStatus == LoanCaseStatus.RevisionRequestedByCredit ||
                                  h.Action.Contains("Revision")), ct);
            revisionCount += await dbContext.GuaranteeCaseWorkflowHistories.AsNoTracking()
                .CountAsync(h => h.CreatedAt >= since && h.Action.Contains("Revision"), ct);
        }
        else if (departmentKey.Equals("Legal", StringComparison.OrdinalIgnoreCase))
        {
            revisionCount = await dbContext.LoanCaseWorkflowHistories.AsNoTracking()
                .CountAsync(h => h.CreatedAt >= since &&
                                 (h.ToStatus == LoanCaseStatus.RevisionRequestedByLegal ||
                                  h.Action.Contains("Revision")), ct);
            revisionCount += await dbContext.CaseWorkflowHistories.AsNoTracking()
                .CountAsync(h => h.CreatedAt >= since && h.Action.Contains("Revision"), ct);
        }
        else if (departmentKey.Equals("Financial", StringComparison.OrdinalIgnoreCase))
        {
            revisionCount = await dbContext.LoanCaseWorkflowHistories.AsNoTracking()
                .CountAsync(h => h.CreatedAt >= since &&
                                 (h.ToStatus == LoanCaseStatus.RevisionRequestedByFinancial ||
                                  h.Action.Contains("Revision")), ct);
        }

        var denominator = Math.Max(queueCount, 1);
        return Math.Round(revisionCount * 100.0 / denominator, 1);
    }

    private static int ComputeGuaranteeProgressPercent(int status)
    {
        if (CaseStageOrder.TryGetRank(CaseModuleType.Guarantee, status, out var rank))
        {
            const int maxRank = 18;
            if (status == (int)GuaranteeCaseStatus.AmendmentApproved)
                return 100;
            if (status == (int)GuaranteeCaseStatus.Completed)
                return 100;
            return Math.Clamp((int)Math.Round(rank * 100.0 / maxRank), 5, 95);
        }

        return Math.Clamp(status * 5, 5, 95);
    }

    private static int ComputeProgressPercent(int status, int[] terminalStatuses, int completedStatus)
    {
        if (status == completedStatus)
            return 100;
        if (terminalStatuses.Contains(status))
            return 0;

        return Math.Clamp(status * 5, 5, 95);
    }
}
