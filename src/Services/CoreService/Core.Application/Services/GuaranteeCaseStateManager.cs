using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Domain.Constants;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Core.Domain.Identity;

namespace Core.Application.Services;

public sealed class GuaranteeCaseStateManager : IGuaranteeCaseStateManager
{
    #region Transition Definitions

    private static readonly HashSet<GuaranteeCaseStatus> TerminalStates =
    [
        GuaranteeCaseStatus.Rejected,
        GuaranteeCaseStatus.Cancelled,
        GuaranteeCaseStatus.Archived,
        GuaranteeCaseStatus.AmendmentCompleted
    ];

    private static readonly Dictionary<(GuaranteeCaseStatus Current, GuaranteeWorkflowAction Action, string Role), GuaranteeCaseStatus> Transitions = new()
    {
        { (GuaranteeCaseStatus.Draft, GuaranteeWorkflowAction.Submit, UserRoleClaims.Applicant), GuaranteeCaseStatus.DataEntry },

        { (GuaranteeCaseStatus.DataEntry, GuaranteeWorkflowAction.Submit, UserRoleClaims.Applicant), GuaranteeCaseStatus.CreditReview },
        { (GuaranteeCaseStatus.DataEntry, GuaranteeWorkflowAction.Cancel, UserRoleClaims.Applicant), GuaranteeCaseStatus.Cancelled },

        { (GuaranteeCaseStatus.CreditReview, GuaranteeWorkflowAction.Approve, UserRoleClaims.CreditExpert), GuaranteeCaseStatus.ApprovalFormEntry },
        { (GuaranteeCaseStatus.CreditReview, GuaranteeWorkflowAction.RequestRevision, UserRoleClaims.CreditExpert), GuaranteeCaseStatus.DataEntry },
        { (GuaranteeCaseStatus.CreditReview, GuaranteeWorkflowAction.Reject, UserRoleClaims.CreditExpert), GuaranteeCaseStatus.Rejected },

        { (GuaranteeCaseStatus.ApprovalFormEntry, GuaranteeWorkflowAction.Submit, UserRoleClaims.CreditExpert), GuaranteeCaseStatus.CeoApprovalInitial },
        { (GuaranteeCaseStatus.ApprovalFormEntry, GuaranteeWorkflowAction.Reject, UserRoleClaims.CreditExpert), GuaranteeCaseStatus.Rejected },

        { (GuaranteeCaseStatus.CeoApprovalInitial, GuaranteeWorkflowAction.Approve, UserRoleClaims.Ceo), GuaranteeCaseStatus.WaitingDraftContract },
        { (GuaranteeCaseStatus.CeoApprovalInitial, GuaranteeWorkflowAction.Reject, UserRoleClaims.Ceo), GuaranteeCaseStatus.Rejected },
        { (GuaranteeCaseStatus.CeoApprovalInitial, GuaranteeWorkflowAction.Cancel, UserRoleClaims.Ceo), GuaranteeCaseStatus.Cancelled },

        { (GuaranteeCaseStatus.WaitingDraftContract, GuaranteeWorkflowAction.UploadDraftContract, UserRoleClaims.LegalExpert), GuaranteeCaseStatus.WaitingSignedContractAndAttachments },
        { (GuaranteeCaseStatus.WaitingDraftContract, GuaranteeWorkflowAction.Reject, UserRoleClaims.LegalExpert), GuaranteeCaseStatus.Rejected },

        { (GuaranteeCaseStatus.WaitingSignedContractAndAttachments, GuaranteeWorkflowAction.SubmitSignedPackage, UserRoleClaims.Applicant), GuaranteeCaseStatus.FinancialAttachmentReview },
        { (GuaranteeCaseStatus.WaitingSignedContractAndAttachments, GuaranteeWorkflowAction.Cancel, UserRoleClaims.Applicant), GuaranteeCaseStatus.Cancelled },

        { (GuaranteeCaseStatus.FinancialAttachmentReview, GuaranteeWorkflowAction.ApproveAttachments, UserRoleClaims.FinancialExpert), GuaranteeCaseStatus.WaitingFinalContract },
        { (GuaranteeCaseStatus.FinancialAttachmentReview, GuaranteeWorkflowAction.RequestRevision, UserRoleClaims.FinancialExpert), GuaranteeCaseStatus.WaitingSignedContractAndAttachments },
        { (GuaranteeCaseStatus.FinancialAttachmentReview, GuaranteeWorkflowAction.Reject, UserRoleClaims.FinancialExpert), GuaranteeCaseStatus.Rejected },

        { (GuaranteeCaseStatus.WaitingFinalContract, GuaranteeWorkflowAction.UploadFinalContract, UserRoleClaims.LegalExpert), GuaranteeCaseStatus.CeoApprovalFinal },
        { (GuaranteeCaseStatus.WaitingFinalContract, GuaranteeWorkflowAction.Reject, UserRoleClaims.LegalExpert), GuaranteeCaseStatus.Rejected },

        { (GuaranteeCaseStatus.CeoApprovalFinal, GuaranteeWorkflowAction.Approve, UserRoleClaims.Ceo), GuaranteeCaseStatus.WaitingIssuanceDocuments },
        { (GuaranteeCaseStatus.CeoApprovalFinal, GuaranteeWorkflowAction.Reject, UserRoleClaims.Ceo), GuaranteeCaseStatus.Rejected },
        { (GuaranteeCaseStatus.CeoApprovalFinal, GuaranteeWorkflowAction.Cancel, UserRoleClaims.Ceo), GuaranteeCaseStatus.Cancelled },

        { (GuaranteeCaseStatus.WaitingIssuanceDocuments, GuaranteeWorkflowAction.UploadIssuanceDocuments, UserRoleClaims.FinancialExpert), GuaranteeCaseStatus.Completed },
        { (GuaranteeCaseStatus.WaitingIssuanceDocuments, GuaranteeWorkflowAction.Reject, UserRoleClaims.FinancialExpert), GuaranteeCaseStatus.Rejected },

        { (GuaranteeCaseStatus.Completed, GuaranteeWorkflowAction.BeginAmendment, UserRoleClaims.Applicant), GuaranteeCaseStatus.AmendmentDraft },
        { (GuaranteeCaseStatus.AmendmentApproved, GuaranteeWorkflowAction.BeginAmendment, UserRoleClaims.Applicant), GuaranteeCaseStatus.AmendmentDraft },
        { (GuaranteeCaseStatus.AmendmentRejected, GuaranteeWorkflowAction.BeginAmendment, UserRoleClaims.Applicant), GuaranteeCaseStatus.AmendmentDraft },
        { (GuaranteeCaseStatus.AmendmentDraft, GuaranteeWorkflowAction.Submit, UserRoleClaims.Applicant), GuaranteeCaseStatus.AmendmentDataEntry },
        { (GuaranteeCaseStatus.AmendmentDataEntry, GuaranteeWorkflowAction.Submit, UserRoleClaims.Applicant), GuaranteeCaseStatus.AmendmentCreditReview },
        { (GuaranteeCaseStatus.AmendmentDataEntry, GuaranteeWorkflowAction.Cancel, UserRoleClaims.Applicant), GuaranteeCaseStatus.AmendmentRejected },
        { (GuaranteeCaseStatus.AmendmentCreditReview, GuaranteeWorkflowAction.Approve, UserRoleClaims.CreditExpert), GuaranteeCaseStatus.AmendmentCeoApproval },
        { (GuaranteeCaseStatus.AmendmentCreditReview, GuaranteeWorkflowAction.RequestRevision, UserRoleClaims.CreditExpert), GuaranteeCaseStatus.AmendmentDataEntry },
        { (GuaranteeCaseStatus.AmendmentCreditReview, GuaranteeWorkflowAction.Reject, UserRoleClaims.CreditExpert), GuaranteeCaseStatus.AmendmentRejected },
        { (GuaranteeCaseStatus.AmendmentCeoApproval, GuaranteeWorkflowAction.Approve, UserRoleClaims.Ceo), GuaranteeCaseStatus.AmendmentLegalReview },
        { (GuaranteeCaseStatus.AmendmentCeoApproval, GuaranteeWorkflowAction.Reject, UserRoleClaims.Ceo), GuaranteeCaseStatus.AmendmentRejected },
        { (GuaranteeCaseStatus.AmendmentLegalReview, GuaranteeWorkflowAction.Approve, UserRoleClaims.LegalExpert), GuaranteeCaseStatus.AmendmentApproved },
        { (GuaranteeCaseStatus.AmendmentLegalReview, GuaranteeWorkflowAction.RequestRevision, UserRoleClaims.LegalExpert), GuaranteeCaseStatus.AmendmentDataEntry },
        { (GuaranteeCaseStatus.AmendmentLegalReview, GuaranteeWorkflowAction.Reject, UserRoleClaims.LegalExpert), GuaranteeCaseStatus.AmendmentRejected }
    };

    static GuaranteeCaseStateManager()
    {
        WorkflowRoleExpander.MirrorUnitManager(Transitions, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager);
        WorkflowRoleExpander.MirrorUnitManager(Transitions, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager);
        WorkflowRoleExpander.MirrorUnitManager(Transitions, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager);
    }

    #endregion

    #region State Machine

    public bool IsTerminalState(GuaranteeCaseStatus status) => TerminalStates.Contains(status);

    public bool CanTransition(
        GuaranteeCaseStatus currentStatus,
        GuaranteeWorkflowAction action,
        string userRole,
        out GuaranteeCaseStatus nextStatus,
        out string errorMessage)
    {
        nextStatus = currentStatus;

        if (IsTerminalState(currentStatus))
        {
            errorMessage = ApiMessages.CannotTransitionFromTerminalState;
            return false;
        }

        if (action == GuaranteeWorkflowAction.Archive)
        {
            if (!string.Equals(userRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = ApiMessages.OnlyAdminCanArchive;
                return false;
            }

            nextStatus = GuaranteeCaseStatus.Archived;
            errorMessage = string.Empty;
            return true;
        }

        if (Transitions.TryGetValue((currentStatus, action, userRole), out var directNext))
        {
            nextStatus = directNext;
            errorMessage = string.Empty;
            return true;
        }

        if (string.Equals(userRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var candidates = Transitions
                .Where(x => x.Key.Current == currentStatus && x.Key.Action == action)
                .Select(x => x.Value)
                .Distinct()
                .ToArray();

            if (candidates.Length == 1)
            {
                nextStatus = candidates[0];
                errorMessage = string.Empty;
                return true;
            }
        }

        errorMessage = ApiMessages.InvalidTransition;
        return false;
    }

    public Task<Result> TransitionAsync(
        GuaranteeCase caseEntity,
        GuaranteeWorkflowAction action,
        string actorId,
        string actorRole,
        string? comment = null,
        Guid? correlationId = null)
    {
        if (caseEntity is null)
            return Task.FromResult(Result.Fail(Error.Unexpected(ApiMessages.CaseEntityIsNull)));

        correlationId ??= Guid.NewGuid();
        if (caseEntity.WorkflowHistory.Any(x => x.CorrelationId == correlationId.Value))
            return Task.FromResult(Result.Ok());

        if (action == GuaranteeWorkflowAction.Archive && caseEntity.CurrentStatus == GuaranteeCaseStatus.Archived)
            return Task.FromResult(Result.Ok());

        if (!CanTransition(caseEntity.CurrentStatus, action, actorRole, out var nextStatus, out var errorMessage))
        {
            if (string.Equals(errorMessage, ApiMessages.OnlyAdminCanArchive, StringComparison.Ordinal))
                return Task.FromResult(Result.Fail(Error.Forbidden(errorMessage)));

            return Task.FromResult(Result.Fail(Error.Conflict(errorMessage)));
        }

        if (nextStatus == caseEntity.CurrentStatus)
            return Task.FromResult(Result.Ok());

        if (!ValidateBusinessRules(caseEntity, action, ref nextStatus, out var businessError))
            return Task.FromResult(Result.Fail(Error.Conflict(businessError)));

        if (action == GuaranteeWorkflowAction.RequestRevision)
        {
            caseEntity.RequestRevision(nextStatus, actorId, actorRole, action, correlationId.Value, comment ?? string.Empty, isInternal: false);
            return Task.FromResult(Result.Ok());
        }

        caseEntity.TransitionTo(nextStatus, actorId, actorRole, action, correlationId.Value, comment);
        return Task.FromResult(Result.Ok());
    }

    private static bool ValidateBusinessRules(
        GuaranteeCase caseEntity,
        GuaranteeWorkflowAction action,
        ref GuaranteeCaseStatus nextStatus,
        out string errorMessage)
    {
        switch (action)
        {
            case GuaranteeWorkflowAction.BeginAmendment:
                if (caseEntity.CurrentStatus is not (
                        GuaranteeCaseStatus.Completed or
                        GuaranteeCaseStatus.AmendmentApproved or
                        GuaranteeCaseStatus.AmendmentRejected))
                {
                    errorMessage = GuaranteeAmendmentMessages.OnlyForCompletedCases;
                    return false;
                }

                if (HasActiveAmendmentWorkflow(caseEntity))
                {
                    errorMessage = ApiMessages.GuaranteeCancellationAlreadyActive;
                    return false;
                }
                break;

            case GuaranteeWorkflowAction.Submit when caseEntity.CurrentStatus == GuaranteeCaseStatus.DataEntry:
                if (!GuaranteeApplicationCompleteness.IsComplete(caseEntity.Application))
                {
                    errorMessage = ApiMessages.GuaranteeApplicationIncomplete;
                    return false;
                }

                var application = caseEntity.Application!;
                var missingDocs = GuaranteeDocumentRequirements.GetMissingForDataEntrySubmit(
                    application.GuaranteeType,
                    caseEntity.Documents);
                if (missingDocs.Count > 0)
                {
                    errorMessage = GuaranteeDocumentRequirements.FormatDataEntryDocumentsIncompleteMessage(missingDocs);
                    return false;
                }
                break;

            case GuaranteeWorkflowAction.Submit when caseEntity.CurrentStatus == GuaranteeCaseStatus.ApprovalFormEntry:
                if (!GuaranteeApprovalFormCompleteness.IsComplete(caseEntity.ApprovalForm))
                {
                    errorMessage = ApiMessages.GuaranteeApprovalFormIncomplete;
                    return false;
                }
                break;

            case GuaranteeWorkflowAction.Submit when caseEntity.CurrentStatus == GuaranteeCaseStatus.AmendmentDataEntry:
                if (!GuaranteeAmendmentCompleteness.IsComplete(caseEntity))
                {
                    errorMessage = caseEntity.AmendmentType == AmendmentType.Cancellation
                        ? ApiMessages.GuaranteeCancellationDataIncomplete
                        : GuaranteeAmendmentMessages.Incomplete;
                    return false;
                }

                if (caseEntity.AmendmentType == AmendmentType.Cancellation)
                {
                    var missingCancellationDocs = GuaranteeDocumentRequirements.GetMissingForCancellation(caseEntity);
                    if (missingCancellationDocs.Count > 0)
                    {
                        errorMessage = ApiMessages.GuaranteeCancellationDocumentsIncomplete;
                        return false;
                    }

                    nextStatus = caseEntity.AmendmentRequiresCreditReview
                        ? GuaranteeCaseStatus.AmendmentCreditReview
                        : GuaranteeCaseStatus.AmendmentCeoApproval;
                    break;
                }

                if (caseEntity.AmendmentType == AmendmentType.Extension)
                {
                    if (caseEntity.AmendmentRequestedAmount.HasValue)
                    {
                        errorMessage = GuaranteeAmendmentMessages.ReductionAmountInvalid;
                        return false;
                    }

                    var currentValidityTo = caseEntity.Application?.ValidityTo ?? caseEntity.ApprovalForm?.ExpiryDate;
                    if (!caseEntity.AmendmentRequestedValidityTo.HasValue)
                    {
                        errorMessage = GuaranteeAmendmentMessages.ExtensionDateRequired;
                        return false;
                    }

                    if (currentValidityTo.HasValue && caseEntity.AmendmentRequestedValidityTo.Value <= currentValidityTo.Value)
                    {
                        errorMessage = GuaranteeAmendmentMessages.ExtensionDateMustExtend;
                        return false;
                    }
                }

                if (caseEntity.AmendmentType == AmendmentType.Reduction)
                {
                    if (caseEntity.AmendmentRequestedValidityTo.HasValue)
                    {
                        errorMessage = GuaranteeAmendmentMessages.ExtensionDateMustExtend;
                        return false;
                    }

                    var originalAmount = caseEntity.ApprovalForm?.GuaranteeAmount
                                         ?? caseEntity.Application?.RequestedGuaranteeAmount
                                         ?? 0m;

                    if (caseEntity.AmendmentRequestedAmount is not > 0)
                    {
                        errorMessage = GuaranteeAmendmentMessages.ReductionAmountRequired;
                        return false;
                    }

                    if (originalAmount <= 0 || caseEntity.AmendmentRequestedAmount > originalAmount)
                    {
                        errorMessage = GuaranteeAmendmentMessages.ReductionAmountInvalid;
                        return false;
                    }
                }
                break;

            case GuaranteeWorkflowAction.Approve when caseEntity.CurrentStatus == GuaranteeCaseStatus.AmendmentCeoApproval:
                if (caseEntity.AmendmentType == AmendmentType.Cancellation)
                {
                    nextStatus = GuaranteeCaseStatus.Cancelled;
                    break;
                }
                break;

            case GuaranteeWorkflowAction.Approve when caseEntity.CurrentStatus == GuaranteeCaseStatus.AmendmentLegalReview:
                if (caseEntity.AmendmentType == AmendmentType.Cancellation)
                {
                    nextStatus = GuaranteeCaseStatus.Cancelled;
                    break;
                }

                if (caseEntity.AmendmentType is not (AmendmentType.Extension or AmendmentType.Reduction))
                {
                    errorMessage = ApiMessages.InvalidTransition;
                    return false;
                }

                if (!GuaranteeDocumentRequirements.HasAmendmentContract(caseEntity.Documents))
                {
                    errorMessage = ApiMessages.GuaranteeAmendmentContractMissing;
                    return false;
                }

                nextStatus = GuaranteeCaseStatus.AmendmentApproved;
                break;

            case GuaranteeWorkflowAction.UploadDraftContract:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == GuaranteeDocumentType.DraftContract))
                {
                    errorMessage = ApiMessages.GuaranteeDraftContractMissing;
                    return false;
                }
                break;

            case GuaranteeWorkflowAction.SubmitSignedPackage:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == GuaranteeDocumentType.SignedContract))
                {
                    errorMessage = ApiMessages.GuaranteeSignedContractMissing;
                    return false;
                }
                break;

            case GuaranteeWorkflowAction.UploadFinalContract:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == GuaranteeDocumentType.FinalContract))
                {
                    errorMessage = ApiMessages.GuaranteeFinalContractMissing;
                    return false;
                }
                break;

            case GuaranteeWorkflowAction.UploadIssuanceDocuments:
                foreach (var required in GuaranteeDocumentRequirements.RequiredForIssuance)
                {
                    if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == required))
                    {
                        errorMessage = ApiMessages.GuaranteeIssuanceDocumentsIncomplete;
                        return false;
                    }
                }
                break;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool HasActiveAmendmentWorkflow(GuaranteeCase caseEntity)
        => caseEntity.CurrentStatus is
            GuaranteeCaseStatus.AmendmentDraft or
            GuaranteeCaseStatus.AmendmentDataEntry or
            GuaranteeCaseStatus.AmendmentCreditReview or
            GuaranteeCaseStatus.AmendmentCeoApproval or
            GuaranteeCaseStatus.AmendmentLegalReview;

    public IEnumerable<GuaranteeWorkflowAction> GetAllowedActions(GuaranteeCaseStatus currentStatus, string userRole)
    {
        if (IsTerminalState(currentStatus))
            return [];

        if (string.Equals(userRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Transitions
                .Where(x => x.Key.Current == currentStatus)
                .Select(x => x.Key.Action)
                .Append(GuaranteeWorkflowAction.Archive)
                .Distinct()
                .ToArray();
        }

        return Transitions
            .Where(x => x.Key.Current == currentStatus && string.Equals(x.Key.Role, userRole, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Key.Action)
            .Distinct()
            .ToArray();
    }

    #endregion
}
