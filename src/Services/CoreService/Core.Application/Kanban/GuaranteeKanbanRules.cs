using Core.Domain.Constants;
using Core.Domain.Enums;
using Core.Domain.Identity;

namespace Core.Application.Kanban;

public static class GuaranteeKanbanRules
{
    private static readonly GuaranteeCaseStatus[] TerminalStatuses =
    [
        GuaranteeCaseStatus.Completed,
        GuaranteeCaseStatus.AmendmentApproved,
        GuaranteeCaseStatus.AmendmentCompleted,
        GuaranteeCaseStatus.Rejected,
        GuaranteeCaseStatus.Cancelled,
        GuaranteeCaseStatus.Archived
    ];

    private static readonly Dictionary<GuaranteeCaseStatus, string> StatusOwnerRole = new()
    {
        [GuaranteeCaseStatus.Draft] = UserRoleClaims.Applicant,
        [GuaranteeCaseStatus.DataEntry] = UserRoleClaims.Applicant,
        [GuaranteeCaseStatus.CreditReview] = UserRoleClaims.CreditExpert,
        [GuaranteeCaseStatus.ApprovalFormEntry] = UserRoleClaims.CreditExpert,
        [GuaranteeCaseStatus.CeoApprovalInitial] = UserRoleClaims.Ceo,
        [GuaranteeCaseStatus.WaitingDraftContract] = UserRoleClaims.LegalExpert,
        [GuaranteeCaseStatus.WaitingSignedContractAndAttachments] = UserRoleClaims.Applicant,
        [GuaranteeCaseStatus.FinancialAttachmentReview] = UserRoleClaims.FinancialExpert,
        [GuaranteeCaseStatus.WaitingFinalContract] = UserRoleClaims.LegalExpert,
        [GuaranteeCaseStatus.CeoApprovalFinal] = UserRoleClaims.Ceo,
        [GuaranteeCaseStatus.WaitingIssuanceDocuments] = UserRoleClaims.FinancialExpert,
        [GuaranteeCaseStatus.AmendmentDraft] = UserRoleClaims.Applicant,
        [GuaranteeCaseStatus.AmendmentDataEntry] = UserRoleClaims.Applicant,
        [GuaranteeCaseStatus.AmendmentCreditReview] = UserRoleClaims.CreditExpert,
        [GuaranteeCaseStatus.AmendmentCeoApproval] = UserRoleClaims.Ceo,
        [GuaranteeCaseStatus.AmendmentLegalReview] = UserRoleClaims.LegalExpert
    };

    private static readonly Dictionary<string, HashSet<GuaranteeCaseStatus>> ActionStatusesByRole = BuildActionStatusesByRole();
    private static readonly Dictionary<string, HashSet<GuaranteeCaseStatus>> WatchStatusesByRole = BuildWatchStatusesByRole();

    public static bool IsTerminal(GuaranteeCaseStatus status) => TerminalStatuses.Contains(status);

    public static bool IsActionRequired(GuaranteeCaseStatus status, string resolvedRole)
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

    public static bool IsWatching(GuaranteeCaseStatus status, string resolvedRole)
    {
        if (IsTerminal(status) || IsActionRequired(status, resolvedRole))
            return false;

        if (string.Equals(resolvedRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
            return false;

        return WatchStatusesByRole.TryGetValue(resolvedRole, out var statuses) && statuses.Contains(status);
    }

    public static string GetStatusTitle(GuaranteeCaseStatus status, AmendmentType? amendmentType = null)
    {
        if (amendmentType == AmendmentType.Cancellation)
            return GetCancellationStatusTitle(status);

        return GetDefaultStatusTitle(status);
    }

    private static string GetCancellationStatusTitle(GuaranteeCaseStatus status) => status switch
    {
        GuaranteeCaseStatus.AmendmentDraft => "پیش‌نویس ابطال",
        GuaranteeCaseStatus.AmendmentDataEntry => "ثبت اطلاعات ابطال",
        GuaranteeCaseStatus.AmendmentCreditReview => "بررسی اعتباری ابطال",
        GuaranteeCaseStatus.AmendmentCeoApproval => "تأیید مدیرعامل (ابطال)",
        GuaranteeCaseStatus.AmendmentLegalReview => "بررسی حقوقی ابطال",
        GuaranteeCaseStatus.AmendmentApproved => "ابطال تایید شد",
        GuaranteeCaseStatus.AmendmentCompleted => "ابطال تکمیل‌شده",
        GuaranteeCaseStatus.Cancelled => "تکمیل ابطال",
        _ => GetDefaultStatusTitle(status)
    };

    private static string GetDefaultStatusTitle(GuaranteeCaseStatus status) => status switch
    {
        GuaranteeCaseStatus.Draft => "پیش‌نویس",
        GuaranteeCaseStatus.DataEntry => "ورود اطلاعات",
        GuaranteeCaseStatus.CreditReview => "بررسی اعتبارات",
        GuaranteeCaseStatus.ApprovalFormEntry => "فرم تصویب",
        GuaranteeCaseStatus.CeoApprovalInitial => "تایید مدیرعامل (اول)",
        GuaranteeCaseStatus.WaitingDraftContract => "پیش‌قرارداد",
        GuaranteeCaseStatus.WaitingSignedContractAndAttachments => "قرارداد امضاشده",
        GuaranteeCaseStatus.FinancialAttachmentReview => "بررسی مالی مدارک",
        GuaranteeCaseStatus.WaitingFinalContract => "قرارداد نهایی",
        GuaranteeCaseStatus.CeoApprovalFinal => "تایید مدیرعامل (نهایی)",
        GuaranteeCaseStatus.WaitingIssuanceDocuments => "صدور ضمانت‌نامه",
        GuaranteeCaseStatus.AmendmentDraft => "پیش‌نویس اصلاحیه",
        GuaranteeCaseStatus.AmendmentDataEntry => "ثبت اطلاعات اصلاحیه",
        GuaranteeCaseStatus.AmendmentCreditReview => "بررسی اعتباری اصلاحیه",
        GuaranteeCaseStatus.AmendmentCeoApproval => "تایید مدیرعامل (اصلاحیه)",
        GuaranteeCaseStatus.AmendmentLegalReview => "بررسی حقوقی اصلاحیه",
        GuaranteeCaseStatus.AmendmentApproved => "اصلاحیه تایید شد",
        GuaranteeCaseStatus.AmendmentCompleted => "اصلاحیه تکمیل‌شده",
        GuaranteeCaseStatus.Completed => "تکمیل‌شده",
        GuaranteeCaseStatus.Rejected => "رد شده",
        GuaranteeCaseStatus.Cancelled => "لغو شده",
        GuaranteeCaseStatus.Archived => "بایگانی",
        _ => status.ToString()
    };

    public static string GetPhaseTitle(GuaranteeCasePhase phase) => phase switch
    {
        GuaranteeCasePhase.Application => "درخواست",
        GuaranteeCasePhase.CreditAssessment => "اعتبارات",
        GuaranteeCasePhase.Legal => "حقوقی",
        GuaranteeCasePhase.Finance => "مالی",
        GuaranteeCasePhase.Closing => "اختتام",
        _ => phase.ToString()
    };

    public static string GetPendingActionLabel(GuaranteeCaseStatus status, string resolvedRole)
    {
        if (IsActionRequired(status, resolvedRole))
            return "اقدام شما لازم است";

        var owner = StatusOwnerRole.GetValueOrDefault(status);
        return owner is null ? "در جریان بررسی" : $"در انتظار {owner}";
    }

    public static string ResolveWorkflowRole(IReadOnlyCollection<string> roles)
    {
        if (roles.Contains(UserRoleClaims.Admin)) return UserRoleClaims.Admin;
        if (roles.Contains(UserRoleClaims.Ceo) || roles.Contains("CEO", StringComparer.OrdinalIgnoreCase)) return UserRoleClaims.Ceo;
        if (roles.Contains(UserRoleClaims.CreditManager)) return UserRoleClaims.CreditManager;
        if (roles.Contains(UserRoleClaims.CreditExpert)) return UserRoleClaims.CreditExpert;
        if (roles.Contains(UserRoleClaims.LegalManager)) return UserRoleClaims.LegalManager;
        if (roles.Contains(UserRoleClaims.LegalExpert)) return UserRoleClaims.LegalExpert;
        if (roles.Contains(UserRoleClaims.FinancialManager)) return UserRoleClaims.FinancialManager;
        if (roles.Contains(UserRoleClaims.FinancialExpert)) return UserRoleClaims.FinancialExpert;
        if (roles.Contains(UserRoleClaims.Applicant)) return UserRoleClaims.Applicant;
        return roles.FirstOrDefault() ?? string.Empty;
    }

    private static Dictionary<string, HashSet<GuaranteeCaseStatus>> BuildActionStatusesByRole()
    {
        var map = new Dictionary<string, HashSet<GuaranteeCaseStatus>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (status, role) in StatusOwnerRole)
        {
            if (!map.TryGetValue(role, out var set))
            {
                set = [];
                map[role] = set;
            }

            set.Add(status);
        }

        WorkflowRoleExpander.MirrorKanbanRole(map, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager);
        WorkflowRoleExpander.MirrorKanbanRole(map, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager);
        WorkflowRoleExpander.MirrorKanbanRole(map, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager);
        return map;
    }

    private static Dictionary<string, HashSet<GuaranteeCaseStatus>> BuildWatchStatusesByRole()
    {
        var all = Enum.GetValues<GuaranteeCaseStatus>().Where(s => !IsTerminal(s)).ToHashSet();
        var watch = new Dictionary<string, HashSet<GuaranteeCaseStatus>>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in new[]
                 {
                     UserRoleClaims.Ceo,
                     UserRoleClaims.CreditExpert,
                     UserRoleClaims.LegalExpert,
                     UserRoleClaims.FinancialExpert,
                     UserRoleClaims.Applicant
                 })
        {
            if (!ActionStatusesByRole.TryGetValue(role, out var owned))
                watch[role] = all;
            else
                watch[role] = all.Where(s => !owned.Contains(s)).ToHashSet();
        }

        WorkflowRoleExpander.MirrorKanbanRole(watch, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager);
        WorkflowRoleExpander.MirrorKanbanRole(watch, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager);
        WorkflowRoleExpander.MirrorKanbanRole(watch, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager);
        return watch;
    }
}
