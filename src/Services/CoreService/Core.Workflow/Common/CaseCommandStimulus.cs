namespace Core.Workflow.Common;

/// <summary>
/// Bookmark stimulus for workflow-owned case commands.
/// Routing data is stored on the Elsa bookmark metadata; the stimulus only selects the command.
/// </summary>
public sealed class CaseCommandStimulus
{
    public Guid CaseId { get; init; }
    public string CommandName { get; init; } = string.Empty;
}
