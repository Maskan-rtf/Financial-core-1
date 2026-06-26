using Core.Domain.Constants;
using Core.Domain.Enums;
using Core.Domain.Identity;

namespace Core.Application.Kanban;

/// <summary>
/// Role/status ownership for the kanban board. Keep aligned with the investment workflow route provider.
/// </summary>
public static class CaseKanbanRules
{
    private static readonly CaseStatus[] TerminalStatuses =
    [
        CaseStatus.Completed,
        CaseStatus.Rejected,
        CaseStatus.Cancelled,
        CaseStatus.Archived
    ];

    private static readonly Dictionary<CaseStatus, string> StatusOwnerRole = new()
    {
        [CaseStatus.Draft] = UserRoleClaims.Applicant,
        [CaseStatus.DataEntry1] = UserRoleClaims.Applicant,
        [CaseStatus.DataEntry2] = UserRoleClaims.Applicant,
        [CaseStatus.ReviewDataEntry1] = UserRoleClaims.InvestmentExpert,
        [CaseStatus.ReviewDataEntry2] = UserRoleClaims.InvestmentExpert,
        [CaseStatus.InitialValuation] = UserRoleClaims.InvestmentManager,
        [CaseStatus.SecondaryValuation] = UserRoleClaims.InvestmentManager,
        [CaseStatus.WaitingPreliminaryContract] = UserRoleClaims.LegalExpert,
        [CaseStatus.WaitingUserReviewPreliminaryContract] = UserRoleClaims.Applicant,
        [CaseStatus.ContractDrafting] = UserRoleClaims.LegalExpert,
        [CaseStatus.WaitingContractSignature] = UserRoleClaims.LegalExpert,
        [CaseStatus.WaitingSignedContractUpload] = UserRoleClaims.LegalExpert,
        [CaseStatus.WaitingFinancialWorksheet] = UserRoleClaims.InvestmentExpert,
        [CaseStatus.FinancialWorksheetReview] = UserRoleClaims.FinancialExpert,
        [CaseStatus.WaitingCeoApproval] = UserRoleClaims.Ceo,
        [CaseStatus.WaitingPayment] = UserRoleClaims.FinancialExpert
    };

    private static readonly Dictionary<string, HashSet<CaseStatus>> ActionStatusesByRole = BuildActionStatusesByRole();

    private static readonly Dictionary<string, HashSet<CaseStatus>> WatchStatusesByRole = BuildWatchStatusesByRole();

    public static bool IsTerminal(CaseStatus status) => TerminalStatuses.Contains(status);

    public static string? GetStatusOwnerRole(CaseStatus status)
        => StatusOwnerRole.TryGetValue(status, out var role) ? role : null;

    public static string GetStatusTitle(CaseStatus status) =>
        KanbanStatusTitles.TryGetValue(status, out var title) ? title : status.ToString();

    public static string GetPhaseTitle(CasePhase phase) =>
        KanbanPhaseTitles.TryGetValue(phase, out var title) ? title : phase.ToString();

    public static bool IsActionRequired(CaseStatus status, string resolvedRole)
    {
        if (IsTerminal(status))
            return false;

        if (string.Equals(resolvedRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
            return StatusOwnerRole.ContainsKey(status);

        return ActionStatusesByRole.TryGetValue(resolvedRole, out var statuses) && statuses.Contains(status);
    }

    public static IReadOnlyList<int> GetActionRequiredStatusValues(string resolvedRole)
    {
        if (string.Equals(resolvedRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
            return StatusOwnerRole.Keys.Select(s => (int)s).ToList();

        return ActionStatusesByRole.TryGetValue(resolvedRole, out var statuses)
            ? statuses.Select(s => (int)s).ToList()
            : [];
    }

    public static bool IsWatching(CaseStatus status, string resolvedRole)
    {
        if (IsTerminal(status) || IsActionRequired(status, resolvedRole))
            return false;

        if (string.Equals(resolvedRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
            return false;

        return WatchStatusesByRole.TryGetValue(resolvedRole, out var statuses) && statuses.Contains(status);
    }

    public static string GetPendingActionLabel(CaseStatus status, string resolvedRole)
    {
        if (IsActionRequired(status, resolvedRole))
            return KanbanActionHints.TryGetValue(status, out var hint) ? hint : "اقدام شما لازم است";

        var owner = GetStatusOwnerRole(status);
        if (owner is null)
            return "در جریان بررسی";

        return KanbanWaitingHints.TryGetValue((status, owner), out var waiting)
            ? waiting
            : $"در انتظار {RoleLabels.GetValueOrDefault(owner, owner)}";
    }

    public static string ResolveWorkflowRole(IReadOnlyCollection<string> roles)
    {
        if (roles.Contains(UserRoleClaims.Admin)) return UserRoleClaims.Admin;
        if (roles.Contains(UserRoleClaims.Ceo) || roles.Contains("CEO", StringComparer.OrdinalIgnoreCase))
            return UserRoleClaims.Ceo;
        if (roles.Contains(UserRoleClaims.InvestmentManager)) return UserRoleClaims.InvestmentManager;
        if (roles.Contains(UserRoleClaims.InvestmentExpert)) return UserRoleClaims.InvestmentExpert;
        if (roles.Contains(UserRoleClaims.LegalManager)) return UserRoleClaims.LegalManager;
        if (roles.Contains(UserRoleClaims.LegalExpert) || roles.Contains(UserRoleClaims.LegalUnit, StringComparer.OrdinalIgnoreCase))
            return UserRoleClaims.LegalExpert;
        if (roles.Contains(UserRoleClaims.FinancialManager)) return UserRoleClaims.FinancialManager;
        if (roles.Contains(UserRoleClaims.FinancialExpert) || roles.Contains(UserRoleClaims.FinancialUnit, StringComparer.OrdinalIgnoreCase))
            return UserRoleClaims.FinancialExpert;
        if (roles.Contains(UserRoleClaims.TechnicalManager)) return UserRoleClaims.TechnicalManager;
        if (roles.Contains(UserRoleClaims.TechnicalExpert)) return UserRoleClaims.TechnicalExpert;
        if (roles.Contains(UserRoleClaims.Applicant) || roles.Contains("User", StringComparer.OrdinalIgnoreCase))
            return UserRoleClaims.Applicant;
        return roles.FirstOrDefault() ?? string.Empty;
    }

    private static Dictionary<string, HashSet<CaseStatus>> BuildActionStatusesByRole()
    {
        var map = new Dictionary<string, HashSet<CaseStatus>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (status, role) in StatusOwnerRole)
        {
            if (!map.TryGetValue(role, out var set))
            {
                set = [];
                map[role] = set;
            }

            set.Add(status);
        }

        WorkflowRoleExpander.MirrorKanbanRole(map, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager);
        WorkflowRoleExpander.MirrorKanbanRole(map, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager);
        WorkflowRoleExpander.MirrorKanbanRole(map, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager);

        map.TryAdd(UserRoleClaims.TechnicalExpert, []);
        WorkflowRoleExpander.MirrorKanbanRole(map, UserRoleClaims.TechnicalExpert, UserRoleClaims.TechnicalManager);

        return map;
    }

    private static Dictionary<string, HashSet<CaseStatus>> BuildWatchStatusesByRole()
    {
        var active = Enum.GetValues<CaseStatus>().Where(s => !IsTerminal(s)).ToHashSet();

        HashSet<CaseStatus> WatchAllExcept(string role)
        {
            var action = ActionStatusesByRole[role];
            return active.Where(s => !action.Contains(s)).ToHashSet();
        }

        var watch = new Dictionary<string, HashSet<CaseStatus>>(StringComparer.OrdinalIgnoreCase)
        {
            [UserRoleClaims.Applicant] = WatchAllExcept(UserRoleClaims.Applicant),
            [UserRoleClaims.InvestmentExpert] = WatchAllExcept(UserRoleClaims.InvestmentExpert),
            [UserRoleClaims.InvestmentManager] = WatchAllExcept(UserRoleClaims.InvestmentManager),
            [UserRoleClaims.LegalExpert] = WatchAllExcept(UserRoleClaims.LegalExpert),
            [UserRoleClaims.FinancialExpert] = WatchAllExcept(UserRoleClaims.FinancialExpert),
            [UserRoleClaims.TechnicalExpert] = WatchAllExcept(UserRoleClaims.TechnicalExpert),
            [UserRoleClaims.Ceo] = WatchAllExcept(UserRoleClaims.Ceo)
        };

        WorkflowRoleExpander.MirrorKanbanRole(watch, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager);
        WorkflowRoleExpander.MirrorKanbanRole(watch, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager);
        WorkflowRoleExpander.MirrorKanbanRole(watch, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager);
        WorkflowRoleExpander.MirrorKanbanRole(watch, UserRoleClaims.TechnicalExpert, UserRoleClaims.TechnicalManager);

        return watch;
    }

    private static readonly Dictionary<CaseStatus, string> KanbanStatusTitles = new()
    {
        [CaseStatus.Draft] = "پیش‌نویس",
        [CaseStatus.DataEntry1] = "فرم اولیه",
        [CaseStatus.ReviewDataEntry1] = "بررسی فرم اولیه",
        [CaseStatus.DataEntry2] = "فرم تکمیلی",
        [CaseStatus.ReviewDataEntry2] = "بررسی فرم تکمیلی",
        [CaseStatus.InitialValuation] = "ارزش‌گذاری اولیه",
        [CaseStatus.SecondaryValuation] = "ارزش‌گذاری ثانویه",
        [CaseStatus.WaitingPreliminaryContract] = "آپلود پیش‌قرارداد",
        [CaseStatus.WaitingUserReviewPreliminaryContract] = "بازبینی پیش‌قرارداد",
        [CaseStatus.ContractDrafting] = "تدوین قرارداد",
        [CaseStatus.WaitingContractSignature] = "امضای قرارداد",
        [CaseStatus.WaitingSignedContractUpload] = "آپلود قرارداد امضاشده",
        [CaseStatus.WaitingFinancialWorksheet] = "کاربرگ مالی",
        [CaseStatus.FinancialWorksheetReview] = "بررسی کاربرگ مالی",
        [CaseStatus.WaitingCeoApproval] = "تأیید مدیرعامل",
        [CaseStatus.WaitingPayment] = "پرداخت",
        [CaseStatus.Completed] = "تکمیل‌شده",
        [CaseStatus.Rejected] = "رد شده",
        [CaseStatus.Cancelled] = "لغو شده",
        [CaseStatus.Archived] = "بایگانی"
    };

    private static readonly Dictionary<CasePhase, string> KanbanPhaseTitles = new()
    {
        [CasePhase.Application] = "درخواست",
        [CasePhase.Valuation] = "ارزش‌گذاری",
        [CasePhase.Legal] = "حقوقی",
        [CasePhase.Finance] = "مالی",
        [CasePhase.Closing] = "اختتام"
    };

    private static readonly Dictionary<CaseStatus, string> KanbanActionHints = new()
    {
        [CaseStatus.Draft] = "ارسال یا تکمیل پیش‌نویس",
        [CaseStatus.DataEntry1] = "تکمیل و ارسال فرم اولیه",
        [CaseStatus.DataEntry2] = "تکمیل و ارسال فرم تکمیلی",
        [CaseStatus.ReviewDataEntry1] = "بررسی و تأیید/اصلاح فرم اولیه",
        [CaseStatus.ReviewDataEntry2] = "بررسی و تأیید/اصلاح فرم تکمیلی",
        [CaseStatus.InitialValuation] = "ثبت/تأیید ارزش‌گذاری اولیه",
        [CaseStatus.SecondaryValuation] = "ثبت/تأیید ارزش‌گذاری ثانویه",
        [CaseStatus.WaitingPreliminaryContract] = "بارگذاری پیش‌قرارداد",
        [CaseStatus.WaitingUserReviewPreliminaryContract] = "بازبینی پیش‌قرارداد",
        [CaseStatus.ContractDrafting] = "تدوین پیش‌نویس قرارداد",
        [CaseStatus.WaitingContractSignature] = "تأیید آماده‌سازی امضا",
        [CaseStatus.WaitingSignedContractUpload] = "بارگذاری قرارداد امضاشده",
        [CaseStatus.WaitingFinancialWorksheet] = "تکمیل و ارسال کاربرگ مالی",
        [CaseStatus.FinancialWorksheetReview] = "بررسی کاربرگ مالی",
        [CaseStatus.WaitingCeoApproval] = "تأیید نهایی مدیرعامل",
        [CaseStatus.WaitingPayment] = "ثبت/تأیید پرداخت"
    };

    private static readonly Dictionary<(CaseStatus Status, string OwnerRole), string> KanbanWaitingHints = new()
    {
        [(CaseStatus.ReviewDataEntry1, UserRoleClaims.InvestmentExpert)] = "در انتظار بررسی کارشناس سرمایه‌گذاری",
        [(CaseStatus.ReviewDataEntry2, UserRoleClaims.InvestmentExpert)] = "در انتظار بررسی کارشناس سرمایه‌گذاری",
        [(CaseStatus.InitialValuation, UserRoleClaims.InvestmentManager)] = "در انتظار ارزش‌گذاری مدیر",
        [(CaseStatus.SecondaryValuation, UserRoleClaims.InvestmentManager)] = "در انتظار ارزش‌گذاری مدیر",
        [(CaseStatus.WaitingPreliminaryContract, UserRoleClaims.LegalExpert)] = "در انتظار واحد حقوقی",
        [(CaseStatus.WaitingUserReviewPreliminaryContract, UserRoleClaims.Applicant)] = "در انتظار متقاضی",
        [(CaseStatus.FinancialWorksheetReview, UserRoleClaims.FinancialExpert)] = "در انتظار واحد مالی",
        [(CaseStatus.WaitingCeoApproval, UserRoleClaims.Ceo)] = "در انتظار تأیید مدیرعامل",
        [(CaseStatus.WaitingPayment, UserRoleClaims.FinancialExpert)] = "در انتظار واحد مالی"
    };

    private static readonly Dictionary<string, string> RoleLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [UserRoleClaims.Applicant] = "متقاضی",
        [UserRoleClaims.InvestmentExpert] = "کارشناس سرمایه‌گذاری",
        [UserRoleClaims.InvestmentManager] = "مدیر سرمایه‌گذاری",
        [UserRoleClaims.LegalExpert] = "کارشناس حقوقی",
        [UserRoleClaims.LegalManager] = "مدیر حقوقی",
        [UserRoleClaims.FinancialExpert] = "کارشناس مالی",
        [UserRoleClaims.FinancialManager] = "مدیر مالی",
        [UserRoleClaims.TechnicalExpert] = "کارشناس فنی",
        [UserRoleClaims.TechnicalManager] = "مدیر فنی",
        [UserRoleClaims.Ceo] = "مدیرعامل",
        [UserRoleClaims.Admin] = "مدیر سیستم"
    };
}
