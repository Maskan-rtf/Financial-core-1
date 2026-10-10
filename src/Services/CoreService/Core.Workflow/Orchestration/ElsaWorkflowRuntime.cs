using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Domain.Enums;
using Core.Workflow.Workflows;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime.Filters;
using Elsa.Workflows.Runtime.Messages;
using IWorkflowResumer = Elsa.Workflows.Runtime.IWorkflowResumer;
using IElsaWorkflowRuntime = Elsa.Workflows.Runtime.IWorkflowRuntime;
using IProcessWorkflowRuntime = Core.Application.Abstractions.IWorkflowRuntime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Workflow.Orchestration;

public sealed class ElsaWorkflowRuntime(
    ICaseWorkflowOrchestrator investmentWorkflowOrchestrator,
    IGuaranteeWorkflowOrchestrator guaranteeWorkflowOrchestrator,
    ILoanWorkflowOrchestrator loanWorkflowOrchestrator,
    IInvestmentWorkflowCommandExecutor investmentWorkflowCommandExecutor,
    IGuaranteeWorkflowCommandExecutor guaranteeWorkflowCommandExecutor,
    ILoanWorkflowCommandExecutor loanWorkflowCommandExecutor,
    ElsaCommandBookmarkReader commandBookmarkReader,
    IWorkflowResumer workflowResumer,
    IElsaWorkflowRuntime elsaWorkflowRuntime,
    ICoreDbContext dbContext,
    ILogger<ElsaWorkflowRuntime> logger) : IProcessWorkflowRuntime
{
    public async Task<Result<WorkflowStartResult>> StartAsync(
        ProcessStartCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var instanceId = command.Module switch
            {
                CaseModuleType.Investment => await investmentWorkflowOrchestrator.StartAsync(command.CaseId, cancellationToken),
                CaseModuleType.Guarantee => await guaranteeWorkflowOrchestrator.StartGuaranteeCaseAsync(command.CaseId, cancellationToken),
                CaseModuleType.Loan => await loanWorkflowOrchestrator.StartLoanCaseAsync(command.CaseId, cancellationToken),
                _ => null
            };

            return string.IsNullOrWhiteSpace(instanceId)
                ? Result<WorkflowStartResult>.Fail(Error.Validation(ApiMessages.InvalidCaseModule))
                : Result<WorkflowStartResult>.Ok(new WorkflowStartResult(instanceId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Elsa workflow start failed for module {Module} case {CaseId}", command.Module, command.CaseId);
            return Result<WorkflowStartResult>.Fail(Error.Unexpected(ApiMessages.UnexpectedError));
        }
    }

    public async Task<Result> SignalAsync(
        ProcessSignalCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var processCommand = command.ProcessCommand;
            switch (processCommand.Module)
            {
                case CaseModuleType.Investment:
                    var investmentExecution = await ExecuteInvestmentCommandAsync(processCommand, cancellationToken);
                    if (investmentExecution.IsFailure)
                        return investmentExecution;

                    return Result.Ok();

                case CaseModuleType.Guarantee:
                    var guaranteeExecution = await ExecuteGuaranteeCommandAsync(processCommand, cancellationToken);
                    if (guaranteeExecution.IsFailure)
                        return guaranteeExecution;

                    return Result.Ok();

                case CaseModuleType.Loan:
                    var execution = await ExecuteLoanCommandAsync(processCommand, cancellationToken);
                    if (execution.IsFailure)
                        return execution;

                    return Result.Ok();

                default:
                    return Result.Fail(Error.Validation(ApiMessages.InvalidCaseModule));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Elsa workflow signal failed for module {Module} case {CaseId}",
                command.ProcessCommand.Module,
                command.ProcessCommand.CaseId);

            return Result.Fail(Error.Unexpected(ApiMessages.UnexpectedError));
        }
    }

    private async Task<Result> ExecuteLoanCommandAsync(
        ProcessCommand processCommand,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LoanWorkflowAction>(processCommand.CommandName, ignoreCase: true, out var action))
            return Result.Fail(Error.Validation(ApiMessages.InvalidTransition));

        if (string.IsNullOrWhiteSpace(processCommand.ActorId))
            return Result.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        if (string.IsNullOrWhiteSpace(processCommand.ActorRole))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var bookmark = await commandBookmarkReader.FindCommandAsync(
            GetWorkflowInstanceId(processCommand.CaseId),
            processCommand.CaseId,
            action.ToString(),
            processCommand.ActorRole,
            cancellationToken);
        if (bookmark.IsFailure)
            return Result.Fail(bookmark.Error!);

        if (!Enum.TryParse<LoanCaseStatus>(bookmark.Value!.TargetStatus, out var targetStatus))
            return Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));

        var payload = processCommand.Payload as LoanWorkflowCommandPayload;
        var execution = await loanWorkflowCommandExecutor.ExecuteAsync(
            new LoanWorkflowExecutionCommand(
                processCommand.CaseId,
                action,
                targetStatus,
                processCommand.ActorId,
                processCommand.ActorRole,
                processCommand.CorrelationId ?? Guid.NewGuid(),
                processCommand.Comment,
                payload?.InternalComment),
            cancellationToken);

        if (execution.IsFailure)
            return execution;

        await BurnAndResumeAsync(GetWorkflowInstanceId(processCommand.CaseId), bookmark.Value!.BookmarkId, cancellationToken);
        return Result.Ok();
    }

    private async Task<Result> ExecuteGuaranteeCommandAsync(
        ProcessCommand processCommand,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<GuaranteeWorkflowAction>(processCommand.CommandName, ignoreCase: true, out var action))
            return Result.Fail(Error.Validation(ApiMessages.InvalidTransition));

        if (string.IsNullOrWhiteSpace(processCommand.ActorId))
            return Result.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        if (string.IsNullOrWhiteSpace(processCommand.ActorRole))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var workflowInstanceId = GetWorkflowInstanceId(processCommand.CaseId);
        var bookmarks = await commandBookmarkReader.FindCommandsAsync(
            workflowInstanceId,
            processCommand.CaseId,
            action.ToString(),
            processCommand.ActorRole,
            cancellationToken);
        if (bookmarks.IsFailure)
        {
            var open = await commandBookmarkReader.ListCommandsAsync(workflowInstanceId, cancellationToken);
            if (open.Count == 0 && await TryRealignGuaranteeWorkflowAsync(processCommand.CaseId, cancellationToken))
            {
                bookmarks = await commandBookmarkReader.FindCommandsAsync(
                    workflowInstanceId,
                    processCommand.CaseId,
                    action.ToString(),
                    processCommand.ActorRole,
                    cancellationToken);
            }
        }

        if (bookmarks.IsFailure)
            return Result.Fail(bookmarks.Error!);

        var bookmark = bookmarks.Value!.First();

        if (!Enum.TryParse<GuaranteeCaseStatus>(bookmark.TargetStatus, out var targetStatus))
            return Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));

        var payload = processCommand.Payload as GuaranteeWorkflowCommandPayload;
        var execution = await guaranteeWorkflowCommandExecutor.ExecuteAsync(
            new GuaranteeWorkflowExecutionCommand(
                processCommand.CaseId,
                action,
                targetStatus,
                processCommand.ActorId,
                processCommand.ActorRole,
                processCommand.CorrelationId ?? Guid.NewGuid(),
                processCommand.Comment,
                payload?.InternalComment),
            cancellationToken);

        if (execution.IsFailure)
            return execution;

        var persistedStatus = await dbContext.GuaranteeCases
            .AsNoTracking()
            .Where(x => x.Id == processCommand.CaseId)
            .Select(x => (GuaranteeCaseStatus?)x.CurrentStatus)
            .FirstOrDefaultAsync(cancellationToken);

        if (persistedStatus is not null)
        {
            var actualBookmark = bookmarks.Value!.FirstOrDefault(x =>
                string.Equals(x.TargetStatus, persistedStatus.Value.ToString(), StringComparison.OrdinalIgnoreCase));
            if (actualBookmark is not null)
                bookmark = actualBookmark;
        }

        await BurnAndResumeAsync(GetWorkflowInstanceId(processCommand.CaseId), bookmark.BookmarkId, cancellationToken);
        return Result.Ok();
    }

    private async Task<Result> ExecuteInvestmentCommandAsync(
        ProcessCommand processCommand,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<WorkflowAction>(processCommand.CommandName, ignoreCase: true, out var action))
            return Result.Fail(Error.Validation(ApiMessages.InvalidTransition));

        if (string.IsNullOrWhiteSpace(processCommand.ActorId))
            return Result.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        if (string.IsNullOrWhiteSpace(processCommand.ActorRole))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var bookmark = await commandBookmarkReader.FindCommandAsync(
            GetWorkflowInstanceId(processCommand.CaseId),
            processCommand.CaseId,
            action.ToString(),
            processCommand.ActorRole,
            cancellationToken);
        if (bookmark.IsFailure)
            return Result.Fail(bookmark.Error!);

        if (!Enum.TryParse<CaseStatus>(bookmark.Value!.TargetStatus, out var targetStatus))
            return Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));

        var payload = processCommand.Payload as InvestmentWorkflowCommandPayload;
        var execution = await investmentWorkflowCommandExecutor.ExecuteAsync(
            new InvestmentWorkflowExecutionCommand(
                processCommand.CaseId,
                action,
                targetStatus,
                processCommand.ActorId,
                processCommand.ActorRole,
                processCommand.CorrelationId ?? Guid.NewGuid(),
                processCommand.Comment,
                payload?.InternalComment),
            cancellationToken);

        if (execution.IsFailure)
            return execution;

        await BurnAndResumeAsync(GetWorkflowInstanceId(processCommand.CaseId), bookmark.Value!.BookmarkId, cancellationToken);
        return Result.Ok();
    }

    /// <summary>
    /// A case can sit on data entry while its workflow has no open command.
    /// That happens after draft is consumed and the join back into data entry never parks.
    /// Recreate the instance and, when the case is already in data entry, step the workflow forward once.
    /// </summary>
    private async Task<bool> TryRealignGuaranteeWorkflowAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var status = await dbContext.GuaranteeCases
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => (GuaranteeCaseStatus?)x.CurrentStatus)
            .FirstOrDefaultAsync(cancellationToken);

        if (status is not (GuaranteeCaseStatus.Draft or GuaranteeCaseStatus.DataEntry))
            return false;

        var workflowInstanceId = GetWorkflowInstanceId(caseId);
        try
        {
            var client = await elsaWorkflowRuntime.CreateClientAsync(workflowInstanceId, cancellationToken);
            if (await client.InstanceExistsAsync(cancellationToken))
                await client.DeleteAsync(cancellationToken);

            await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
            {
                WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionId(
                    GuaranteeCaseWorkflow.DefinitionId,
                    Elsa.Common.Models.VersionOptions.Latest),
                CorrelationId = workflowInstanceId,
                Input = new Dictionary<string, object> { ["CaseId"] = caseId }
            }, cancellationToken);

            if (status == GuaranteeCaseStatus.DataEntry)
            {
                var open = await commandBookmarkReader.ListCommandsAsync(workflowInstanceId, cancellationToken);
                var enterDataEntry = open.FirstOrDefault(x =>
                    x.CommandName.Equals(nameof(GuaranteeWorkflowAction.Submit), StringComparison.OrdinalIgnoreCase)
                    && x.TargetStatus.Equals(nameof(GuaranteeCaseStatus.DataEntry), StringComparison.OrdinalIgnoreCase));
                if (enterDataEntry is null)
                    return false;

                await BurnAndResumeAsync(workflowInstanceId, enterDataEntry.BookmarkId, cancellationToken);
            }

            var parked = await commandBookmarkReader.ListCommandsAsync(workflowInstanceId, cancellationToken);
            logger.LogInformation(
                "Realigned guarantee workflow for case {CaseId} at status {Status} with {BookmarkCount} open commands",
                caseId,
                status,
                parked.Count);
            return parked.Count > 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not realign guarantee workflow for case {CaseId}", caseId);
            return false;
        }
    }

    private async Task BurnAndResumeAsync(
        string workflowInstanceId,
        string selectedBookmarkId,
        CancellationToken cancellationToken)
    {
        await commandBookmarkReader.BurnSiblingCommandBookmarksAsync(workflowInstanceId, selectedBookmarkId, cancellationToken);
        await workflowResumer.ResumeAsync(new BookmarkFilter { BookmarkId = selectedBookmarkId }, options: null, cancellationToken);
    }

    private static string GetWorkflowInstanceId(Guid caseId) => caseId.ToString("D");
}
