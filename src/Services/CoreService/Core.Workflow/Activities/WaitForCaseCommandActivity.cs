using System.Text.Json;
using Core.Workflow.Common;
using Elsa.Workflows;
using Elsa.Workflows.Models;

namespace Core.Workflow.Activities;

public sealed class WaitForCaseCommandActivity : Elsa.Workflows.Activity
{
    public const string CaseIdMetadataKey = "CaseId";
    public const string TargetStatusMetadataKey = "TargetStatus";
    public const string AllowedRolesMetadataKey = "AllowedRoles";
    public const string CommandNameMetadataKey = "CommandName";

    public Input<Guid> CaseId { get; set; } = default!;
    public Input<string> CommandName { get; set; } = default!;
    public Input<string> TargetStatus { get; set; } = default!;
    public Input<string> AllowedRoles { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // Workflow input is only reliable on the execution that started the instance.
        // After a resume, GetInput<Guid>("CaseId") often comes back empty, so the next
        // bookmark would not match the case and submit would return 409.
        var caseId = ResolveCaseId(context);
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
                [CaseIdMetadataKey] = caseId.ToString("D"),
                [CommandNameMetadataKey] = commandName,
                [TargetStatusMetadataKey] = targetStatus,
                [AllowedRolesMetadataKey] = allowedRoles
            }
        });

        await ValueTask.CompletedTask;
    }

    private Guid ResolveCaseId(ActivityExecutionContext context)
    {
        var fromInput = context.Get(CaseId);
        if (fromInput != Guid.Empty)
            return fromInput;

        var input = context.WorkflowExecutionContext.Input;
        if (input is not null && input.TryGetValue("CaseId", out var raw) && TryCoerceGuid(raw, out var coerced))
            return coerced;

        if (Guid.TryParse(context.WorkflowExecutionContext.CorrelationId, out var fromCorrelation) && fromCorrelation != Guid.Empty)
            return fromCorrelation;

        return Guid.Empty;
    }

    private static bool TryCoerceGuid(object? value, out Guid caseId)
    {
        switch (value)
        {
            case Guid guid when guid != Guid.Empty:
                caseId = guid;
                return true;
            case string text when Guid.TryParse(text, out var parsed) && parsed != Guid.Empty:
                caseId = parsed;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.String
                && Guid.TryParse(element.GetString(), out var fromJson)
                && fromJson != Guid.Empty:
                caseId = fromJson;
                return true;
            default:
                caseId = Guid.Empty;
                return false;
        }
    }

    private static async ValueTask OnResumeAsync(ActivityExecutionContext context)
    {
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}
