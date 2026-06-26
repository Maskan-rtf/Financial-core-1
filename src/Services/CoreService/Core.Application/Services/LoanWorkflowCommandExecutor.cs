using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.Notifications.Sms;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public sealed class LoanWorkflowCommandExecutor(
    ICoreUnitOfWork unitOfWork,
    ICoreDbContext dbContext,
    IClock clock,
    IWorkflowSmsNotifier workflowSmsNotifier,
    ILogger<LoanWorkflowCommandExecutor> logger) : ILoanWorkflowCommandExecutor
{
    public async Task<Result> ExecuteAsync(LoanWorkflowExecutionCommand command, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.LoanCases.GetAsync(command.CaseId, cancellationToken);
        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        if (entity.WorkflowHistory.Any(x => x.CorrelationId == command.CorrelationId))
            return Result.Ok();

        if (command.TargetStatus == entity.CurrentStatus)
            return Result.Ok();

        var statusBefore = entity.CurrentStatus;
        var phaseBefore = entity.CurrentPhase;
        var commentsCountBefore = entity.Comments.Count;

        var validation = ValidateBusinessRules(entity, command.Action);
        if (validation.IsFailure)
            return validation;

        if (command.Action == LoanWorkflowAction.RequestRevision)
        {
            entity.RequestRevision(
                command.TargetStatus,
                command.ActorId,
                command.ActorRole,
                command.Action,
                command.CorrelationId,
                command.Comment ?? string.Empty,
                isInternal: false);
        }
        else
        {
            entity.TransitionTo(
                command.TargetStatus,
                command.ActorId,
                command.ActorRole,
                command.Action,
                command.CorrelationId,
                command.Comment);
        }

        if (!string.IsNullOrWhiteSpace(command.InternalComment) && SupportsInternalComment(command.Action, statusBefore))
            entity.AddDiscussionComment(phaseBefore, command.ActorId, command.ActorRole, command.InternalComment, false, true);

        var persist = await PersistTransitionAsync(entity, commentsCountBefore, cancellationToken);
        if (persist.IsFailure)
            return persist;

        await NotifyWorkflowSmsSafeAsync(
            entity.Id,
            entity.ApplicantUserId,
            entity.CaseNumber,
            (int)statusBefore,
            (int)entity.CurrentStatus,
            cancellationToken);

        return Result.Ok();
    }

    private static Result ValidateBusinessRules(LoanCase caseEntity, LoanWorkflowAction action)
    {
        switch (action)
        {
            case LoanWorkflowAction.Submit when caseEntity.CurrentStatus is LoanCaseStatus.DataEntry or LoanCaseStatus.RevisionRequestedByCredit:
                if (!LoanApplicationCompleteness.IsComplete(caseEntity.Application))
                    return Result.Fail(Error.Conflict(ApiMessages.LoanApplicationIncomplete));

                var missingDocs = LoanDocumentRequirements.GetMissingForDataEntrySubmit(caseEntity.Documents);
                if (missingDocs.Count > 0)
                    return Result.Fail(Error.Conflict(LoanDocumentRequirements.FormatDataEntryDocumentsIncompleteMessage(missingDocs)));
                break;

            case LoanWorkflowAction.Approve when caseEntity.CurrentStatus == LoanCaseStatus.PendingCreditReview:
                if (!LoanApprovalDetailCompleteness.IsComplete(caseEntity.ApprovalDetail))
                    return Result.Fail(Error.Conflict(ApiMessages.LoanApprovalDetailIncomplete));
                break;

            case LoanWorkflowAction.SubmitInstallments:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == LoanDocumentType.RawContract))
                    return Result.Fail(Error.Conflict(ApiMessages.LoanRawContractMissing));

                if (caseEntity.Installments.Count == 0)
                    return Result.Fail(Error.Conflict(ApiMessages.LoanInstallmentsMissing));
                break;

            case LoanWorkflowAction.SubmitSignedPackage:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == LoanDocumentType.SignedContract))
                    return Result.Fail(Error.Conflict(ApiMessages.LoanSignedContractMissing));
                break;

            case LoanWorkflowAction.UploadFinalContract:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == LoanDocumentType.FinalContract))
                    return Result.Fail(Error.Conflict(ApiMessages.LoanFinalContractMissing));
                break;

            case LoanWorkflowAction.Approve when caseEntity.CurrentStatus == LoanCaseStatus.RepaymentPhase:
                if (caseEntity.Installments.Any(x => !x.IsPaid && !x.IsGracePeriod))
                    return Result.Fail(Error.Conflict(ApiMessages.LoanRepaymentIncomplete));
                break;
        }

        return Result.Ok();
    }

    private static bool SupportsInternalComment(LoanWorkflowAction action, LoanCaseStatus statusBefore) =>
        action switch
        {
            LoanWorkflowAction.Approve => statusBefore is
                LoanCaseStatus.PendingCreditReview or
                LoanCaseStatus.PendingLegalFinalReview or
                LoanCaseStatus.PendingFinancialReview,
            _ => false
        };

    private async Task<Result> PersistTransitionAsync(LoanCase entity, int commentsCountBefore, CancellationToken cancellationToken)
    {
        var history = entity.WorkflowHistory[^1];
        var pendingComments = entity.Comments.Skip(commentsCountBefore).ToList();

        if (dbContext is DbContext ef)
            ef.ChangeTracker.Clear();

        var rows = await dbContext.LoanCases.ApplyStateAsync(
            entity.Id,
            entity.CurrentStatus,
            entity.CurrentPhase,
            entity.UpdatedAt ?? clock.UtcNow,
            entity.CompletedAt,
            cancellationToken);

        if (rows == 0)
            return Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        await dbContext.LoanCaseWorkflowHistories.AddAsync(history, cancellationToken);
        foreach (var comment in pendingComments)
            await dbContext.LoanCaseComments.AddAsync(comment, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private async Task NotifyWorkflowSmsSafeAsync(
        Guid caseId,
        string applicantUserId,
        string caseNumber,
        int fromStatus,
        int toStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            await workflowSmsNotifier.NotifyStepChangeAsync(
                new WorkflowSmsNotification(
                    CaseModuleType.Loan,
                    caseId,
                    applicantUserId,
                    caseNumber,
                    fromStatus,
                    toStatus),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workflow SMS notification failed for loan case {CaseId}", caseId);
        }
    }
}
