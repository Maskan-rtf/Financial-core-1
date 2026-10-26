using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Common;
using Core.Domain.Identity;
using Core.Workflow.Activities;
using Core.Workflow.Common;
using Elsa.Extensions;
using Elsa.Workflows.Helpers;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Entities;
using Elsa.Workflows.Runtime.Filters;
using Microsoft.Extensions.Logging;

namespace Core.Workflow.Orchestration;

public sealed class ElsaCommandBookmarkReader(
    IBookmarkStore bookmarkStore,
    ILogger<ElsaCommandBookmarkReader> logger)
{
    private static readonly string ActivityTypeName = ActivityTypeNameHelper.GenerateTypeName<WaitForCaseCommandActivity>();

    public async Task<Result<ElsaCommandBookmark>> FindCommandAsync(
        string workflowInstanceId,
        Guid caseId,
        string commandName,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var commands = await FindCommandsAsync(workflowInstanceId, caseId, commandName, actorRole, cancellationToken);
        if (commands.IsFailure)
            return Result<ElsaCommandBookmark>.Fail(commands.Error!);

        return Result<ElsaCommandBookmark>.Ok(commands.Value!.First());
    }

    public async Task<Result<IReadOnlyCollection<ElsaCommandBookmark>>> FindCommandsAsync(
        string workflowInstanceId,
        Guid caseId,
        string commandName,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var bookmarks = await GetCommandBookmarksAsync(workflowInstanceId, cancellationToken);
        var matchingCommand = bookmarks
            .Where(x => MatchesCommand(x, caseId, commandName))
            .ToArray();

        if (matchingCommand.Length == 0)
        {
            if (bookmarks.Count == 0)
            {
                logger.LogWarning(
                    "Workflow {WorkflowInstanceId} for case {CaseId} has no open step, so command {CommandName} was rejected",
                    workflowInstanceId,
                    caseId,
                    commandName);
                return Result<IReadOnlyCollection<ElsaCommandBookmark>>.Fail(
                    Error.Conflict(ApiMessages.WorkflowStepUnavailable(DescribeCommand(commandName))));
            }

            logger.LogWarning(
                "Command {CommandName} is not open on workflow {WorkflowInstanceId} for case {CaseId}. Open commands: {OpenCommands}",
                commandName,
                workflowInstanceId,
                caseId,
                string.Join(", ", bookmarks.Select(DescribeBookmark).Where(x => x.Length > 0).Distinct()));
            return Result<IReadOnlyCollection<ElsaCommandBookmark>>.Fail(Error.Conflict(ApiMessages.WorkflowActionNotOpen));
        }

        var authorized = matchingCommand
            .Select(ToCommandBookmark)
            .Where(x => x is not null)
            .Cast<ElsaCommandBookmark>()
            .Where(x => RoleMatches(x.AllowedRoles, actorRole))
            .ToArray();

        return authorized.Length == 0
            ? Result<IReadOnlyCollection<ElsaCommandBookmark>>.Fail(Error.Forbidden(ApiMessages.NotAllowed))
            : Result<IReadOnlyCollection<ElsaCommandBookmark>>.Ok(authorized);
    }

    public async Task<IReadOnlyCollection<ElsaCommandBookmark>> GetAllowedCommandsAsync(
        string? workflowInstanceId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workflowInstanceId))
            return [];

        var bookmarks = await GetCommandBookmarksAsync(workflowInstanceId, cancellationToken);
        return bookmarks
            .Select(ToCommandBookmark)
            .Where(x => x is not null)
            .Cast<ElsaCommandBookmark>()
            .Where(x => RoleMatches(x.AllowedRoles, actorRole))
            .GroupBy(x => x.CommandName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ElsaCommandBookmark>> ListCommandsAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken)
    {
        var bookmarks = await GetCommandBookmarksAsync(workflowInstanceId, cancellationToken);
        return bookmarks
            .Select(ToCommandBookmark)
            .Where(x => x is not null)
            .Cast<ElsaCommandBookmark>()
            .ToArray();
    }

    public async Task BurnSiblingCommandBookmarksAsync(
        string workflowInstanceId,
        string selectedBookmarkId,
        CancellationToken cancellationToken)
    {
        var bookmarks = await GetCommandBookmarksAsync(workflowInstanceId, cancellationToken);

        foreach (var bookmark in bookmarks.Where(x => !string.Equals(x.Id, selectedBookmarkId, StringComparison.Ordinal)))
            await bookmarkStore.DeleteAsync(new BookmarkFilter { BookmarkId = bookmark.Id }, cancellationToken);
    }

    private async Task<IReadOnlyCollection<StoredBookmark>> GetCommandBookmarksAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken)
        => (await bookmarkStore.FindManyAsync(
            new BookmarkFilter { WorkflowInstanceId = workflowInstanceId, Name = ActivityTypeName },
            cancellationToken)).ToArray();

    private static bool MatchesCommand(StoredBookmark bookmark, Guid caseId, string commandName)
    {
        var payload = bookmark.GetPayload<CaseCommandStimulus>();
        var actualCommand = FirstNonEmpty(
            GetMetadata(bookmark, WaitForCaseCommandActivity.CommandNameMetadataKey),
            payload?.CommandName ?? string.Empty);
        if (!string.Equals(actualCommand, commandName, StringComparison.OrdinalIgnoreCase))
            return false;

        // Bookmarks are already loaded for this case's workflow instance.
        // A resumed step can persist Guid.Empty when the original CaseId input does not round-trip.
        var bookmarkCaseId = ResolveBookmarkCaseId(bookmark, payload);
        return bookmarkCaseId == Guid.Empty || bookmarkCaseId == caseId;
    }

    private static ElsaCommandBookmark? ToCommandBookmark(StoredBookmark bookmark)
    {
        var payload = bookmark.GetPayload<CaseCommandStimulus>();
        var commandName = FirstNonEmpty(
            GetMetadata(bookmark, WaitForCaseCommandActivity.CommandNameMetadataKey),
            payload?.CommandName ?? string.Empty);
        var targetStatus = GetMetadata(bookmark, WaitForCaseCommandActivity.TargetStatusMetadataKey);
        var allowedRoles = GetMetadata(bookmark, WaitForCaseCommandActivity.AllowedRolesMetadataKey)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (string.IsNullOrWhiteSpace(commandName) || string.IsNullOrWhiteSpace(targetStatus))
            return null;

        return new ElsaCommandBookmark(bookmark.Id, ResolveBookmarkCaseId(bookmark, payload), commandName, targetStatus, allowedRoles);
    }

    private static Guid ResolveBookmarkCaseId(StoredBookmark bookmark, CaseCommandStimulus? payload)
    {
        if (payload is not null && payload.CaseId != Guid.Empty)
            return payload.CaseId;

        return Guid.TryParse(GetMetadata(bookmark, WaitForCaseCommandActivity.CaseIdMetadataKey), out var metadataCaseId)
            ? metadataCaseId
            : Guid.Empty;
    }

    private static string FirstNonEmpty(string? primary, string fallback)
        => string.IsNullOrWhiteSpace(primary) ? fallback : primary;

    private static string GetMetadata(StoredBookmark bookmark, string key)
        => bookmark.Metadata is not null && bookmark.Metadata.TryGetValue(key, out var value)
            ? Convert.ToString(value) ?? string.Empty
            : string.Empty;

    private static string DescribeBookmark(StoredBookmark bookmark)
    {
        var payload = bookmark.GetPayload<CaseCommandStimulus>();
        return payload?.CommandName ?? string.Empty;
    }

    private static string DescribeCommand(string commandName) => commandName.ToLowerInvariant() switch
    {
        "submit" => "ارسال",
        "approve" => "تأیید",
        "requestrevision" => "درخواست اصلاح",
        "reject" => "رد",
        "cancel" => "لغو",
        "archive" => "بایگانی",
        "uploadpreliminarycontract" => "بارگذاری پیش‌قرارداد",
        "uploadsignedcontract" => "بارگذاری قرارداد امضاشده",
        "submitfinancialworksheet" => "ارسال کاربرگ مالی",
        "approvefinancialworksheet" => "تأیید کاربرگ مالی",
        "completepayment" => "تکمیل پرداخت",
        "finalizecontractdraft" => "نهایی‌کردن پیش‌نویس قرارداد",
        "confirmsignature" => "تأیید امضا",
        _ => "این عمل"
    };

    private static bool RoleMatches(IReadOnlyCollection<string> allowedRoles, string actorRole)
    {
        if (allowedRoles.Count == 0)
            return true;

        if (string.Equals(actorRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
            return true;

        return allowedRoles.Any(x => string.Equals(x, actorRole, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record ElsaCommandBookmark(
    string BookmarkId,
    Guid CaseId,
    string CommandName,
    string TargetStatus,
    IReadOnlyCollection<string> AllowedRoles);
