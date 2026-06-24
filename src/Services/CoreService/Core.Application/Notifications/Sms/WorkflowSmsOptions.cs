namespace Core.Application.Notifications.Sms;

public sealed class WorkflowSmsOptions
{
    public const string SectionName = "WorkflowSms";

    public bool Enabled { get; set; } = true;

    /// <summary>Relative to content root (e.g. Config/workflow-sms.config.json).</summary>
    public string ConfigFilePath { get; set; } = "Config/workflow-sms.config.json";
}
