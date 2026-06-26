using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.Notifications.Sms;
using Core.Domain.Constants;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public sealed class InvestmentWorkflowCommandExecutor(
    ICoreUnitOfWork unitOfWork,
    ICoreDbContext dbContext,
    IClock clock,
    ICaseWorkflowSmsNotifier workflowSmsNotifier,
    ILogger<InvestmentWorkflowCommandExecutor> logger) : IInvestmentWorkflowCommandExecutor
{
    public async Task<Result> ExecuteAsync(InvestmentWorkflowExecutionCommand command, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.InvestmentCases.GetAsync(command.CaseId, cancellationToken);
        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));

        if (entity.WorkflowHistory.Any(x => x.CorrelationId == command.CorrelationId))
            return Result.Ok();

        if (command.TargetStatus == entity.CurrentStatus)
            return Result.Ok();

        var statusBefore = entity.CurrentStatus;
        var phaseBefore = entity.CurrentPhase;
        var commentsCountBefore = entity.Comments.Count;

        var validation = ValidateBusinessRules(entity, command.Action, command.TargetStatus);
        if (validation.IsFailure)
            return validation;

        if (command.Action == WorkflowAction.RequestRevision)
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

        if (!string.IsNullOrWhiteSpace(command.InternalComment) && SupportsInternalApproveComment(command.Action, statusBefore))
            entity.AddDiscussionComment(phaseBefore, command.ActorId, command.ActorRole, command.InternalComment, false, true);

        var persist = await PersistCaseTransitionAsync(entity, commentsCountBefore, cancellationToken);
        if (persist.IsFailure)
            return persist;

        await NotifyWorkflowSmsSafeAsync(
            entity.Id,
            entity.ApplicantUserId,
            entity.CaseNumber,
            statusBefore,
            entity.CurrentStatus,
            command.Action,
            cancellationToken);

        return Result.Ok();
    }

    private static Result ValidateBusinessRules(InvestmentCase caseEntity, WorkflowAction action, CaseStatus nextStatus)
    {
        switch (action)
        {
            case WorkflowAction.Submit when caseEntity.CurrentStatus == CaseStatus.DataEntry1:
                if (caseEntity.ApplicantProfile is null)
                    return Result.Fail(Error.Conflict(ApiMessages.CannotSubmitDataEntry1BeforeSave));

                if (string.IsNullOrWhiteSpace(caseEntity.ApplicantProfile.RepresentativeFullName) ||
                    string.IsNullOrWhiteSpace(caseEntity.ApplicantProfile.ContactEmail) ||
                    caseEntity.ApplicantProfile.RequestedAmount <= 0 ||
                    caseEntity.ApplicantProfile.BusinessStage is not (BusinessStage.Idea or BusinessStage.HasPrototype))
                    return Result.Fail(Error.Conflict(ApiMessages.DataEntry1Incomplete));

                if (!caseEntity.Documents.Any(d => d.DocumentType == DocumentType.PitchDeck))
                    return Result.Fail(Error.Conflict(ApiMessages.DataEntry1PitchDeckRequired));
                break;

            case WorkflowAction.Submit when caseEntity.CurrentStatus == CaseStatus.DataEntry2:
                if (caseEntity.AttractionBasis is null)
                    return Result.Fail(Error.Conflict(ApiMessages.CannotSubmitDataEntry2BeforeSave));

                if (string.IsNullOrWhiteSpace(caseEntity.AttractionBasis.InvestmentAttractionBasis))
                    return Result.Fail(Error.Conflict(ApiMessages.DataEntry2Incomplete));

                var missingDoc = DataEntry2DocumentRequirements.RequiredForSubmit
                    .FirstOrDefault(t => !caseEntity.Documents.Any(d => d.DocumentType == t));
                if (missingDoc != default)
                    return Result.Fail(Error.Conflict(ApiMessages.DataEntry2DocumentsIncomplete));
                break;

            case WorkflowAction.UploadPreliminaryContract when nextStatus == CaseStatus.WaitingUserReviewPreliminaryContract:
                if (!caseEntity.Documents.Any(x => x.DocumentType == DocumentType.PreContract))
                    return Result.Fail(Error.Conflict(ApiMessages.PreliminaryContractMissing));
                break;

            case WorkflowAction.UploadSignedContract when nextStatus == CaseStatus.WaitingFinancialWorksheet:
                if (!caseEntity.Documents.Any(x => x.DocumentType == DocumentType.SignedContract))
                    return Result.Fail(Error.Conflict(ApiMessages.SignedContractMissing));
                break;

            case WorkflowAction.SubmitFinancialWorksheet when nextStatus == CaseStatus.FinancialWorksheetReview:
            case WorkflowAction.ApproveFinancialWorksheet when nextStatus == CaseStatus.WaitingCeoApproval:
                if (caseEntity.FinancialWorksheet is null || caseEntity.FinancialWorksheet.ApprovedAmount <= 0)
                    return Result.Fail(Error.Conflict(ApiMessages.FinancialWorksheetMissingOrInvalid));
                break;

            case WorkflowAction.Approve when caseEntity.CurrentStatus == CaseStatus.WaitingCeoApproval:
                if (caseEntity.FinancialWorksheet is null || caseEntity.FinancialWorksheet.ApprovedAmount <= 0)
                    return Result.Fail(Error.Conflict(ApiMessages.FinancialWorksheetMissingOrInvalid));
                break;

            case WorkflowAction.CompletePayment when nextStatus == CaseStatus.Completed:
                if (caseEntity.FinancialWorksheet is null || caseEntity.FinancialWorksheet.ApprovedAmount <= 0)
                    return Result.Fail(Error.Conflict(ApiMessages.ApprovedAmountNotSet));

                var totalConfirmed = caseEntity.Payments
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .Sum(p => p.Amount);
                if (totalConfirmed < caseEntity.FinancialWorksheet.ApprovedAmount)
                    return Result.Fail(Error.Conflict(ApiMessages.PaymentsIncomplete));
                break;
        }

        return Result.Ok();
    }

    private static bool SupportsInternalApproveComment(WorkflowAction action, CaseStatus statusBefore) =>
        action switch
        {
            WorkflowAction.Approve => statusBefore is CaseStatus.ReviewDataEntry1 or CaseStatus.ReviewDataEntry2,
            WorkflowAction.ApproveFinancialWorksheet => statusBefore == CaseStatus.FinancialWorksheetReview,
            _ => false
        };

    private async Task<Result> PersistCaseTransitionAsync(
        InvestmentCase entity,
        int commentsCountBefore,
        CancellationToken cancellationToken)
    {
        var history = entity.WorkflowHistory[^1];
        var pendingComments = entity.Comments.Skip(commentsCountBefore).ToList();

        if (dbContext is DbContext efContext)
            efContext.ChangeTracker.Clear();

        var rows = await dbContext.InvestmentCases.ApplyStateAsync(
            entity.Id,
            entity.CurrentStatus,
            entity.CurrentPhase,
            entity.UpdatedAt ?? clock.UtcNow,
            entity.CompletedAt,
            cancellationToken);

        if (rows == 0)
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));

        await dbContext.CaseWorkflowHistories.AddAsync(history, cancellationToken);
        foreach (var pendingComment in pendingComments)
            await dbContext.CaseComments.AddAsync(pendingComment, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private async Task NotifyWorkflowSmsSafeAsync(
        Guid caseId,
        string applicantUserId,
        string caseNumber,
        CaseStatus from,
        CaseStatus to,
        WorkflowAction action,
        CancellationToken cancellationToken)
    {
        try
        {
            await workflowSmsNotifier.NotifyStatusChangeAsync(
                caseId, applicantUserId, caseNumber, from, to, action, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workflow SMS notification failed for case {CaseId}", caseId);
        }
    }
}
