using Core.Workflow.Common;
using Elsa.Workflows;
using Elsa.Workflows.Models;

namespace Core.Workflow.Activities;

public sealed class WaitForCaseCommandActivity : Elsa.Workflows.Activity
{
    public const string TargetStatusMetadataKey = "TargetStatus";
    public const string AllowedRolesMetadataKey = "AllowedRoles";
    public const string CommandNameMetadataKey = "CommandName";

    public Input<Guid> CaseId { get; set; } = default!;
    public Input<string> CommandName { get; set; } = default!;
    public Input<string> TargetStatus { get; set; } = default!;
    public Input<string> AllowedRoles { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var caseId = context.Get(CaseId);
        var commandName = context.Get(CommandName) ?? string.Empty;
        var targetStatus = context.Get(TargetStatus) ?? string.Empty;
        var allowedRoles = context.Get(AllowedRoles) ?? string.Empty;

        var stimulus = new CaseCommandStimulus
        {
            CaseId = caseId,
            CommandName = commandName
        };

        context.CreateBookmark(new CreateBookmarkArgs
        {
            Stimulus = stimulus,
            AutoBurn = false,
            IncludeActivityInstanceId = false,
            Callback = OnResumeAsync,
            Metadata = new Dictionary<string, string>
            {
                [CommandNameMetadataKey] = commandName,
                [TargetStatusMetadataKey] = targetStatus,
                [AllowedRolesMetadataKey] = allowedRoles
            }
        });

        await ValueTask.CompletedTask;
    }

    private static async ValueTask OnResumeAsync(ActivityExecutionContext context)
    {
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}
