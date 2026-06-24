using Core.Domain.Enums;

namespace Core.Application.Notifications.Sms;

public interface IWorkflowSmsCatalog
{
    bool IsGloballyEnabled { get; }

    string Signature { get; }

    WorkflowSmsStepConfig? FindStep(CaseModuleType module, int toStatus);
}
