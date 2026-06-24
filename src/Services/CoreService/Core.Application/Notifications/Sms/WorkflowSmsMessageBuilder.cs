using Core.Application.Kanban;
using Core.Application.Notifications.Sms;
using Core.Domain.Enums;

namespace Core.Application.Notifications.Sms;

public static class WorkflowSmsMessageBuilder
{
    public static string Build(
        WorkflowSmsStepConfig step,
        string signature,
        string caseNumber,
        string statusTitle)
    {
        var message = step.Message
            .Replace("{caseNumber}", caseNumber, StringComparison.Ordinal)
            .Replace("{statusTitle}", statusTitle, StringComparison.Ordinal);

        if (step.ApplicantActionRequired && !string.IsNullOrWhiteSpace(step.ApplicantActionHint))
        {
            message += "\n" + step.ApplicantActionHint
                .Replace("{caseNumber}", caseNumber, StringComparison.Ordinal)
                .Replace("{statusTitle}", statusTitle, StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(signature))
            message += "\n" + signature;

        return message;
    }

    public static string ResolveStatusTitle(CaseModuleType module, int status)
        => module switch
        {
            CaseModuleType.Investment when Enum.IsDefined(typeof(CaseStatus), status)
                => CaseKanbanRules.GetStatusTitle((CaseStatus)status),
            CaseModuleType.Guarantee when Enum.IsDefined(typeof(GuaranteeCaseStatus), status)
                => GuaranteeKanbanRules.GetStatusTitle((GuaranteeCaseStatus)status),
            CaseModuleType.Loan when Enum.IsDefined(typeof(LoanCaseStatus), status)
                => LoanKanbanRules.GetStatusTitle((LoanCaseStatus)status),
            _ => status.ToString()
        };
}
