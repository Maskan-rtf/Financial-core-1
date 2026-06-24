using Core.Application.Abstractions;
using Core.Application.DTOs;
using Core.Application.Kanban;
using Core.Domain.Enums;
using Core.Domain.Identity;

namespace Core.Application.Mappers;

public sealed class KanbanDtoMapper : IKanbanDtoMapper
{
    private const string InvestmentApiBase = "/api/v1/investmentcases";
    private const string GuaranteeApiBase = "/api/v1/guaranteecases";
    private const string LoanApiBase = "/api/v1/loancases";

    public KanbanCaseCardDto MapInvestmentActionCard(
        KanbanCaseProjection projection,
        string role,
        IReadOnlyList<string> allowedActions)
        => new(
            projection.Id,
            projection.CaseNumber,
            CaseModuleType.Investment,
            InvestmentApiBase,
            nameof(CaseStatus),
            (int)projection.CurrentStatus,
            CaseKanbanRules.GetPhaseTitle(projection.CurrentPhase),
            CaseKanbanRules.GetStatusTitle(projection.CurrentStatus),
            projection.ApplicantType,
            projection.StartupTitle,
            projection.CompanyName,
            projection.ApplicantFullName,
            projection.CreatedAt,
            projection.UpdatedAt,
            CaseKanbanRules.GetPendingActionLabel(projection.CurrentStatus, role),
            allowedActions);

    public KanbanCaseCardDto MapGuaranteeActionCard(
        GuaranteeKanbanCaseProjection projection,
        string role,
        IReadOnlyList<string> allowedActions)
        => new(
            projection.Id,
            projection.CaseNumber,
            CaseModuleType.Guarantee,
            GuaranteeApiBase,
            nameof(GuaranteeCaseStatus),
            (int)projection.CurrentStatus,
            GuaranteeKanbanRules.GetPhaseTitle(projection.CurrentPhase),
            GuaranteeKanbanRules.GetStatusTitle(projection.CurrentStatus, projection.AmendmentType),
            projection.ApplicantType,
            projection.RepresentativeName,
            projection.CompanyName,
            projection.ApplicantFullName,
            projection.CreatedAt,
            projection.UpdatedAt,
            GuaranteeKanbanRules.GetPendingActionLabel(projection.CurrentStatus, role),
            allowedActions);

    public KanbanCaseCardDto MapRenewalActionCard(GuaranteeRenewalKanbanProjection projection)
        => new(
            projection.ParentGuaranteeCaseId,
            projection.CaseNumber,
            CaseModuleType.Guarantee,
            GuaranteeApiBase,
            nameof(GuaranteeRenewalStatus),
            (int)projection.CurrentStatus,
            "تمدید",
            GetRenewalStatusTitle(projection.CurrentStatus),
            null,
            projection.ParentCaseNumber,
            null,
            null,
            projection.CreatedAt,
            projection.UpdatedAt,
            "اقدام شما لازم است",
            []);

    public KanbanCaseCardDto MapLoanActionCard(
        LoanKanbanCaseProjection projection,
        string role,
        IReadOnlyList<string> allowedActions)
        => new(
            projection.Id,
            projection.CaseNumber,
            CaseModuleType.Loan,
            LoanApiBase,
            nameof(LoanCaseStatus),
            (int)projection.CurrentStatus,
            LoanKanbanRules.GetPhaseTitle(projection.CurrentPhase),
            LoanKanbanRules.GetStatusTitle(projection.CurrentStatus),
            projection.ApplicantType,
            projection.RequestedAmount?.ToString("N0"),
            projection.CompanyName,
            projection.ApplicantFullName,
            projection.CreatedAt,
            projection.UpdatedAt,
            LoanKanbanRules.GetPendingActionLabel(projection.CurrentStatus, role),
            allowedActions);

    public KanbanCaseSummaryDto MapInvestmentWatchCard(KanbanCaseProjection projection, string role)
        => new(
            projection.Id,
            projection.CaseNumber,
            CaseModuleType.Investment,
            InvestmentApiBase,
            nameof(CaseStatus),
            (int)projection.CurrentStatus,
            CaseKanbanRules.GetPhaseTitle(projection.CurrentPhase),
            CaseKanbanRules.GetStatusTitle(projection.CurrentStatus),
            projection.StartupTitle,
            projection.CreatedAt,
            CaseKanbanRules.GetPendingActionLabel(projection.CurrentStatus, role));

    public KanbanCaseSummaryDto MapGuaranteeWatchCard(GuaranteeKanbanCaseProjection projection, string role)
        => new(
            projection.Id,
            projection.CaseNumber,
            CaseModuleType.Guarantee,
            GuaranteeApiBase,
            nameof(GuaranteeCaseStatus),
            (int)projection.CurrentStatus,
            GuaranteeKanbanRules.GetPhaseTitle(projection.CurrentPhase),
            GuaranteeKanbanRules.GetStatusTitle(projection.CurrentStatus, projection.AmendmentType),
            projection.RepresentativeName,
            projection.CreatedAt,
            GuaranteeKanbanRules.GetPendingActionLabel(projection.CurrentStatus, role));

    public KanbanCaseSummaryDto MapRenewalWatchCard(GuaranteeRenewalKanbanProjection projection)
        => new(
            projection.ParentGuaranteeCaseId,
            projection.CaseNumber,
            CaseModuleType.Guarantee,
            GuaranteeApiBase,
            nameof(GuaranteeRenewalStatus),
            (int)projection.CurrentStatus,
            "تمدید",
            GetRenewalStatusTitle(projection.CurrentStatus),
            projection.ParentCaseNumber,
            projection.CreatedAt,
            "در جریان بررسی");

    public KanbanCaseSummaryDto MapLoanWatchCard(LoanKanbanCaseProjection projection, string role)
        => new(
            projection.Id,
            projection.CaseNumber,
            CaseModuleType.Loan,
            LoanApiBase,
            nameof(LoanCaseStatus),
            (int)projection.CurrentStatus,
            LoanKanbanRules.GetPhaseTitle(projection.CurrentPhase),
            LoanKanbanRules.GetStatusTitle(projection.CurrentStatus),
            projection.CompanyName ?? projection.RequestedAmount?.ToString("N0"),
            projection.CreatedAt,
            LoanKanbanRules.GetPendingActionLabel(projection.CurrentStatus, role));

    private static string GetRenewalStatusTitle(GuaranteeRenewalStatus status) => status switch
    {
        GuaranteeRenewalStatus.Draft => "پیش‌نویس تمدید",
        GuaranteeRenewalStatus.CeoReview => "بررسی مدیرعامل",
        GuaranteeRenewalStatus.CreditDateUpdate => "به‌روزرسانی تاریخ",
        GuaranteeRenewalStatus.Completed => "تکمیل",
        GuaranteeRenewalStatus.Rejected => "رد شده",
        GuaranteeRenewalStatus.Cancelled => "لغو",
        _ => status.ToString()
    };
}
