using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Domain.Constants;
using Core.Domain.Entities;
using Core.Domain.Entities.Guarantee;
using Core.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public sealed class GuaranteeWorkflowCommandExecutor(
    ICoreUnitOfWork unitOfWork,
    IClock clock,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<GuaranteeWorkflowCommandExecutor> logger) : IGuaranteeWorkflowCommandExecutor
{
    public async Task<Result> ExecuteAsync(GuaranteeWorkflowExecutionCommand command, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.GuaranteeCases.GetAsync(command.CaseId, cancellationToken);
        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        if (entity.WorkflowHistory.Any(x => x.CorrelationId == command.CorrelationId))
            return Result.Ok();

        var targetStatus = command.TargetStatus;
        if (targetStatus == entity.CurrentStatus)
            return Result.Ok();

        var statusBefore = entity.CurrentStatus;
        var phaseBefore = entity.CurrentPhase;
        var historyCountBefore = entity.WorkflowHistory.Count;
        var commentsCountBefore = entity.Comments.Count;

        var validation = ValidateBusinessRules(entity, command.Action, ref targetStatus);
        if (validation.IsFailure)
            return validation;

        if (command.Action == GuaranteeWorkflowAction.RequestRevision)
        {
            entity.RequestRevision(
                targetStatus,
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
                targetStatus,
                command.ActorId,
                command.ActorRole,
                command.Action,
                command.CorrelationId,
                command.Comment);
        }

        if (!string.IsNullOrWhiteSpace(command.InternalComment) && SupportsInternalComment(command.Action, statusBefore))
            entity.AddDiscussionComment(phaseBefore, command.ActorId, command.ActorRole, command.InternalComment, false, true);

        if (entity.CurrentStatus is GuaranteeCaseStatus.AmendmentApproved or GuaranteeCaseStatus.Cancelled)
            entity.ApplyApprovedAmendment();

        if (entity.WorkflowHistory.Count <= historyCountBefore)
            return Result.Ok();

        var persist = await PersistTransitionAsync(entity, commentsCountBefore, historyCountBefore, cancellationToken);
        if (persist.IsFailure)
            return persist;

        foreach (var historyEntry in entity.WorkflowHistory.Skip(historyCountBefore))
        {
            WorkflowSmsBackgroundNotifier.NotifyGuaranteeStepChange(
                serviceScopeFactory,
                logger,
                entity.Id,
                entity.ApplicantUserId,
                entity.CaseNumber,
                (int)historyEntry.FromStatus,
                (int)historyEntry.ToStatus);
        }

        if (entity.CurrentStatus == GuaranteeCaseStatus.ApprovalFormEntry)
        {
            var seed = await EnsureApprovalFormSeededAsync(entity, cancellationToken);
            if (seed.IsFailure)
                return seed;
        }

        return Result.Ok();
    }

    private static Result ValidateBusinessRules(
        GuaranteeCase caseEntity,
        GuaranteeWorkflowAction action,
        ref GuaranteeCaseStatus nextStatus)
    {
        switch (action)
        {
            case GuaranteeWorkflowAction.BeginAmendment:
                if (caseEntity.CurrentStatus is not (
                        GuaranteeCaseStatus.Completed or
                        GuaranteeCaseStatus.AmendmentApproved or
                        GuaranteeCaseStatus.AmendmentRejected))
                    return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.OnlyForCompletedCases));

                if (HasActiveAmendmentWorkflow(caseEntity))
                    return Result.Fail(Error.Conflict(ApiMessages.GuaranteeCancellationAlreadyActive));
                break;

            case GuaranteeWorkflowAction.Submit when caseEntity.CurrentStatus == GuaranteeCaseStatus.DataEntry:
                if (!GuaranteeApplicationCompleteness.IsComplete(caseEntity.Application))
                    return Result.Fail(Error.Conflict(ApiMessages.GuaranteeApplicationIncomplete));

                var application = caseEntity.Application!;
                var missingDocs = GuaranteeDocumentRequirements.GetMissingForDataEntrySubmit(
                    application.GuaranteeType,
                    caseEntity.Documents);
                if (missingDocs.Count > 0)
                    return Result.Fail(Error.Conflict(GuaranteeDocumentRequirements.FormatDataEntryDocumentsIncompleteMessage(missingDocs)));
                break;

            case GuaranteeWorkflowAction.Submit when caseEntity.CurrentStatus == GuaranteeCaseStatus.ApprovalFormEntry:
                if (!GuaranteeApprovalFormCompleteness.IsComplete(caseEntity.ApprovalForm))
                    return Result.Fail(Error.Conflict(ApiMessages.GuaranteeApprovalFormIncomplete));
                break;

            case GuaranteeWorkflowAction.Submit when caseEntity.CurrentStatus == GuaranteeCaseStatus.AmendmentDataEntry:
                if (!GuaranteeAmendmentCompleteness.IsComplete(caseEntity))
                {
                    var message = caseEntity.AmendmentType == AmendmentType.Cancellation
                        ? ApiMessages.GuaranteeCancellationDataIncomplete
                        : GuaranteeAmendmentMessages.Incomplete;
                    return Result.Fail(Error.Conflict(message));
                }

                if (caseEntity.AmendmentType == AmendmentType.Cancellation)
                {
                    var missingCancellationDocs = GuaranteeDocumentRequirements.GetMissingForCancellation(caseEntity);
                    if (missingCancellationDocs.Count > 0)
                        return Result.Fail(Error.Conflict(ApiMessages.GuaranteeCancellationDocumentsIncomplete));

                    nextStatus = caseEntity.AmendmentRequiresCreditReview
                        ? GuaranteeCaseStatus.AmendmentCreditReview
                        : GuaranteeCaseStatus.AmendmentCeoApproval;
                    break;
                }

                if (caseEntity.AmendmentType == AmendmentType.Extension)
                {
                    if (caseEntity.AmendmentRequestedAmount.HasValue)
                        return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.ReductionAmountInvalid));

                    var currentValidityTo = caseEntity.Application?.ValidityTo ?? caseEntity.ApprovalForm?.ExpiryDate;
                    if (!caseEntity.AmendmentRequestedValidityTo.HasValue)
                        return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.ExtensionDateRequired));

                    if (currentValidityTo.HasValue && caseEntity.AmendmentRequestedValidityTo.Value <= currentValidityTo.Value)
                        return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.ExtensionDateMustExtend));
                }

                if (caseEntity.AmendmentType == AmendmentType.Reduction)
                {
                    if (caseEntity.AmendmentRequestedValidityTo.HasValue)
                        return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.ExtensionDateMustExtend));

                    var originalAmount = caseEntity.ApprovalForm?.GuaranteeAmount
                                         ?? caseEntity.Application?.RequestedGuaranteeAmount
                                         ?? 0m;

                    if (caseEntity.AmendmentRequestedAmount is not > 0)
                        return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.ReductionAmountRequired));

                    if (originalAmount <= 0 || caseEntity.AmendmentRequestedAmount > originalAmount)
                        return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.ReductionAmountInvalid));
                }
                break;

            case GuaranteeWorkflowAction.Approve when caseEntity.CurrentStatus == GuaranteeCaseStatus.AmendmentCeoApproval:
                if (caseEntity.AmendmentType == AmendmentType.Cancellation)
                    nextStatus = GuaranteeCaseStatus.Cancelled;
                break;

            case GuaranteeWorkflowAction.Approve when caseEntity.CurrentStatus == GuaranteeCaseStatus.AmendmentLegalReview:
                if (caseEntity.AmendmentType == AmendmentType.Cancellation)
                {
                    nextStatus = GuaranteeCaseStatus.Cancelled;
                    break;
                }

                if (caseEntity.AmendmentType is not (AmendmentType.Extension or AmendmentType.Reduction))
                    return Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));

                if (!GuaranteeDocumentRequirements.HasAmendmentContract(caseEntity.Documents))
                    return Result.Fail(Error.Conflict(ApiMessages.GuaranteeAmendmentContractMissing));

                nextStatus = GuaranteeCaseStatus.AmendmentApproved;
                break;

            case GuaranteeWorkflowAction.UploadDraftContract:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == GuaranteeDocumentType.DraftContract))
                    return Result.Fail(Error.Conflict(ApiMessages.GuaranteeDraftContractMissing));
                break;

            case GuaranteeWorkflowAction.SubmitSignedPackage:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == GuaranteeDocumentType.SignedContract))
                    return Result.Fail(Error.Conflict(ApiMessages.GuaranteeSignedContractMissing));
                break;

            case GuaranteeWorkflowAction.UploadFinalContract:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == GuaranteeDocumentType.FinalContract))
                    return Result.Fail(Error.Conflict(ApiMessages.GuaranteeFinalContractMissing));
                break;

            case GuaranteeWorkflowAction.UploadIssuanceDocuments:
                foreach (var required in GuaranteeDocumentRequirements.RequiredForIssuance)
                {
                    if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == required))
                        return Result.Fail(Error.Conflict(ApiMessages.GuaranteeIssuanceDocumentsIncomplete));
                }
                break;
        }

        return Result.Ok();
    }

    private async Task<Result> PersistTransitionAsync(
        GuaranteeCase entity,
        int commentsCountBefore,
        int historyCountBefore,
        CancellationToken cancellationToken)
    {
        var pendingHistory = entity.WorkflowHistory.Skip(historyCountBefore).ToList();
        var pendingComments = entity.Comments.Skip(commentsCountBefore).ToList();
        var pendingNewAmendmentRecords = unitOfWork.GuaranteeCases.CapturePendingNewAmendmentHistory();

        unitOfWork.GuaranteeCases.ClearChangeTracker();

        foreach (var history in pendingHistory)
        {
            var auditApplied = await ApplyAmendmentAuditDecisionAsync(entity, history, cancellationToken);
            if (auditApplied.IsFailure)
                return auditApplied;
        }

        var rows = await unitOfWork.GuaranteeCases.ApplyStateAndAmendmentAsync(
            entity.Id,
            entity.CurrentStatus,
            entity.CurrentPhase,
            entity.UpdatedAt ?? clock.UtcNow,
            entity.CompletedAt,
            entity.AmendmentType,
            entity.AmendmentReason,
            entity.AmendmentRequiresCreditReview,
            entity.AmendmentOriginalGuaranteeReference,
            entity.LegalOverrideApproved,
            entity.SettlementConfirmationRequired,
            entity.AmendmentRequestedValidityTo,
            entity.AmendmentRequestedAmount,
            entity.AmendmentApprovedValidityTo,
            entity.AmendmentApprovedAmount,
            entity.AmendmentCreatedAt,
            entity.AmendmentCompletedAt,
            cancellationToken);

        if (rows == 0)
            return Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        if (entity.CurrentStatus is GuaranteeCaseStatus.AmendmentApproved
            || (entity.CurrentStatus == GuaranteeCaseStatus.Cancelled && entity.AmendmentType == AmendmentType.Cancellation))
        {
            await PersistApprovedAmendmentDataAsync(entity, cancellationToken);
        }

        foreach (var record in pendingNewAmendmentRecords)
            await unitOfWork.GuaranteeCases.InsertAmendmentHistoryRecordAsync(record, cancellationToken);

        foreach (var history in pendingHistory)
            await unitOfWork.GuaranteeCases.InsertWorkflowHistoryAsync(history, cancellationToken);

        foreach (var comment in pendingComments)
            await unitOfWork.GuaranteeCases.InsertCommentAsync(comment, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private async Task<Result> ApplyAmendmentAuditDecisionAsync(
        GuaranteeCase entity,
        GuaranteeCaseWorkflowHistory history,
        CancellationToken cancellationToken)
    {
        if (entity.AmendmentType is not (AmendmentType.Extension or AmendmentType.Reduction or AmendmentType.Cancellation))
            return Result.Ok();

        GuaranteeAmendmentHistoryStatus? decisionStatus = history.Action switch
        {
            nameof(GuaranteeWorkflowAction.Approve)
                when ShouldMarkAmendmentAuditApproved(history.FromStatus, entity.AmendmentType)
                => GuaranteeAmendmentHistoryStatus.Approved,
            nameof(GuaranteeWorkflowAction.Reject) => GuaranteeAmendmentHistoryStatus.Rejected,
            _ => null
        };

        if (decisionStatus is null)
            return Result.Ok();

        var rows = await unitOfWork.GuaranteeCases.ApplyLatestPendingAmendmentAuditDecisionAsync(
            entity.Id,
            decisionStatus.Value,
            history.ChangedByUserId,
            history.CreatedAt,
            history.Comment,
            cancellationToken);

        if (rows == 0)
        {
            var latestStatus = await unitOfWork.GuaranteeCases.GetLatestAmendmentHistoryStatusAsync(entity.Id, cancellationToken);
            return latestStatus == decisionStatus.Value
                ? Result.Ok()
                : Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.Incomplete));
        }

        return Result.Ok();
    }

    private async Task PersistApprovedAmendmentDataAsync(GuaranteeCase entity, CancellationToken cancellationToken)
    {
        var approvedValidityTo = entity.AmendmentApprovedValidityTo ?? entity.AmendmentRequestedValidityTo;
        var approvedAmount = entity.AmendmentApprovedAmount ?? entity.AmendmentRequestedAmount;
        var updatedAt = entity.UpdatedAt ?? clock.UtcNow;

        if (entity.AmendmentType == AmendmentType.Extension && approvedValidityTo.HasValue)
            await unitOfWork.GuaranteeCases.PersistApprovedAmendmentExtensionAsync(entity.Id, approvedValidityTo.Value, updatedAt, cancellationToken);

        if (entity.AmendmentType == AmendmentType.Reduction && approvedAmount.HasValue)
            await unitOfWork.GuaranteeCases.PersistApprovedAmendmentReductionAsync(entity.Id, approvedAmount.Value, updatedAt, cancellationToken);

        if (entity.AmendmentType == AmendmentType.Cancellation)
            await unitOfWork.GuaranteeCases.PersistApprovedAmendmentCancellationAsync(entity.Id, updatedAt, cancellationToken);
    }

    private async Task<Result> EnsureApprovalFormSeededAsync(GuaranteeCase entity, CancellationToken cancellationToken)
    {
        var exists = await unitOfWork.GuaranteeCases.ApprovalFormExistsAsync(entity.Id, cancellationToken);
        if (exists)
            return Result.Ok();

        var application = entity.Application ?? await unitOfWork.GuaranteeCases.GetApplicationByCaseIdAsync(entity.Id, cancellationToken);
        if (application is null)
            return Result.Ok();

        var approvalForm = new GuaranteeApprovalForm(entity.Id);
        var creditSnapshot = await GuaranteeApplicantCreditSnapshotCalculator.ComputeAsync(unitOfWork, entity, cancellationToken);
        GuaranteeApprovalFormMapping.Apply(approvalForm, application, creditSnapshot);

        await unitOfWork.GuaranteeCases.AddApprovalFormAsync(approvalForm, cancellationToken);
        await unitOfWork.GuaranteeCases.TouchUpdatedAtAsync(entity.Id, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static bool SupportsInternalComment(GuaranteeWorkflowAction action, GuaranteeCaseStatus statusBefore) =>
        action switch
        {
            GuaranteeWorkflowAction.Approve => statusBefore is GuaranteeCaseStatus.CreditReview or GuaranteeCaseStatus.AmendmentCreditReview or GuaranteeCaseStatus.AmendmentCeoApproval or GuaranteeCaseStatus.AmendmentLegalReview,
            GuaranteeWorkflowAction.ApproveAttachments => statusBefore == GuaranteeCaseStatus.FinancialAttachmentReview,
            _ => false
        };

    private static bool HasActiveAmendmentWorkflow(GuaranteeCase entity)
        => entity.CurrentStatus is
            GuaranteeCaseStatus.AmendmentDraft or
            GuaranteeCaseStatus.AmendmentDataEntry or
            GuaranteeCaseStatus.AmendmentCreditReview or
            GuaranteeCaseStatus.AmendmentCeoApproval or
            GuaranteeCaseStatus.AmendmentLegalReview;

    private static bool ShouldMarkAmendmentAuditApproved(GuaranteeCaseStatus status, AmendmentType? amendmentType)
        => status switch
        {
            GuaranteeCaseStatus.AmendmentCeoApproval when amendmentType is AmendmentType.Cancellation => true,
            GuaranteeCaseStatus.AmendmentLegalReview when amendmentType is AmendmentType.Extension or AmendmentType.Reduction or AmendmentType.Cancellation => true,
            _ => false
        };
}
