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

namespace Core.Workflow.Orchestration;

public sealed class ElsaCommandBookmarkReader(IBookmarkStore bookmarkStore)
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
            return Result<IReadOnlyCollection<ElsaCommandBookmark>>.Fail(Error.Conflict(ApiMessages.InvalidTransition));

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
        return payload is not null
               && payload.CaseId == caseId
               && string.Equals(payload.CommandName, commandName, StringComparison.OrdinalIgnoreCase);
    }

    private static ElsaCommandBookmark? ToCommandBookmark(StoredBookmark bookmark)
    {
        var payload = bookmark.GetPayload<CaseCommandStimulus>();
        if (payload is null)
            return null;

        var commandName = GetMetadata(bookmark, WaitForCaseCommandActivity.CommandNameMetadataKey);
        var targetStatus = GetMetadata(bookmark, WaitForCaseCommandActivity.TargetStatusMetadataKey);
        var allowedRoles = GetMetadata(bookmark, WaitForCaseCommandActivity.AllowedRolesMetadataKey)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (string.IsNullOrWhiteSpace(commandName) || string.IsNullOrWhiteSpace(targetStatus))
            return null;

        return new ElsaCommandBookmark(bookmark.Id, payload.CaseId, commandName, targetStatus, allowedRoles);
    }

    private static string GetMetadata(StoredBookmark bookmark, string key)
        => bookmark.Metadata is not null && bookmark.Metadata.TryGetValue(key, out var value)
            ? Convert.ToString(value) ?? string.Empty
            : string.Empty;

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
