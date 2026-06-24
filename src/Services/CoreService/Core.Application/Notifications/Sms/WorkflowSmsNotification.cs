using Core.Domain.Enums;

namespace Core.Application.Notifications.Sms;

public sealed record WorkflowSmsNotification(
    CaseModuleType Module,
    Guid CaseId,
    string ApplicantUserId,
    string CaseNumber,
    int FromStatus,
    int ToStatus,
    string? ActionName = null);
