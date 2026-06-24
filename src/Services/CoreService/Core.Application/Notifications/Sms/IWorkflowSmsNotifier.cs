namespace Core.Application.Notifications.Sms;

public interface IWorkflowSmsNotifier
{
    Task NotifyStepChangeAsync(
        WorkflowSmsNotification notification,
        CancellationToken cancellationToken = default);
}
