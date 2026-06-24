namespace Core.Application.Notifications.Sms;

public sealed class WorkflowSmsConfigRoot
{
    public bool GlobalEnabled { get; set; } = true;

    public string Signature { get; set; } =
        "صندوق پژوهش و فناوری غیردولتی مسکن";

    public Dictionary<string, WorkflowSmsModuleConfig> Modules { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorkflowSmsModuleConfig
{
    public List<WorkflowSmsStepConfig> Steps { get; set; } = [];
}

public sealed class WorkflowSmsStepConfig
{
    public int Status { get; set; }

    public string? StatusName { get; set; }

    public bool Enabled { get; set; } = true;

    public string Message { get; set; } = string.Empty;

    public bool ApplicantActionRequired { get; set; }

    public string? ApplicantActionHint { get; set; }
}
