using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Domain.Constants;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Core.Domain.Identity;

namespace Core.Application.Services;

public sealed class LoanCaseStateManager : ILoanCaseStateManager
{
    #region Transition Definitions

    private static readonly HashSet<LoanCaseStatus> TerminalStates =
    [
        LoanCaseStatus.CanceledByCeo,
        LoanCaseStatus.Completed,
        LoanCaseStatus.Archived
    ];

    private static readonly Dictionary<(LoanCaseStatus Current, LoanWorkflowAction Action, string Role), LoanCaseStatus> Transitions = new()
    {
        { (LoanCaseStatus.Draft, LoanWorkflowAction.Submit, UserRoleClaims.Applicant), LoanCaseStatus.DataEntry },

        { (LoanCaseStatus.DataEntry, LoanWorkflowAction.Submit, UserRoleClaims.Applicant), LoanCaseStatus.PendingCreditReview },
        { (LoanCaseStatus.RevisionRequestedByCredit, LoanWorkflowAction.Submit, UserRoleClaims.Applicant), LoanCaseStatus.PendingCreditReview },

        { (LoanCaseStatus.PendingCreditReview, LoanWorkflowAction.Approve, UserRoleClaims.CreditExpert), LoanCaseStatus.PendingCeoInitialApproval },
        { (LoanCaseStatus.PendingCreditReview, LoanWorkflowAction.RequestRevision, UserRoleClaims.CreditExpert), LoanCaseStatus.RevisionRequestedByCredit },

        { (LoanCaseStatus.PendingCeoInitialApproval, LoanWorkflowAction.Approve, UserRoleClaims.Ceo), LoanCaseStatus.PendingLegalRawContract },
        { (LoanCaseStatus.PendingCeoInitialApproval, LoanWorkflowAction.Reject, UserRoleClaims.Ceo), LoanCaseStatus.CanceledByCeo },

        { (LoanCaseStatus.PendingLegalRawContract, LoanWorkflowAction.SubmitInstallments, UserRoleClaims.LegalExpert), LoanCaseStatus.PendingApplicantSignature },

        { (LoanCaseStatus.PendingApplicantSignature, LoanWorkflowAction.SubmitSignedPackage, UserRoleClaims.Applicant), LoanCaseStatus.PendingLegalFinalReview },
        { (LoanCaseStatus.RevisionRequestedByLegal, LoanWorkflowAction.SubmitSignedPackage, UserRoleClaims.Applicant), LoanCaseStatus.PendingLegalFinalReview },
        { (LoanCaseStatus.RevisionRequestedByFinancial, LoanWorkflowAction.SubmitSignedPackage, UserRoleClaims.Applicant), LoanCaseStatus.PendingLegalFinalReview },

        { (LoanCaseStatus.PendingLegalFinalReview, LoanWorkflowAction.Approve, UserRoleClaims.LegalExpert), LoanCaseStatus.PendingFinancialReview },
        { (LoanCaseStatus.PendingLegalFinalReview, LoanWorkflowAction.RequestRevision, UserRoleClaims.LegalExpert), LoanCaseStatus.RevisionRequestedByLegal },

        { (LoanCaseStatus.PendingFinancialReview, LoanWorkflowAction.Approve, UserRoleClaims.FinancialExpert), LoanCaseStatus.PendingLegalFinalContract },
        { (LoanCaseStatus.PendingFinancialReview, LoanWorkflowAction.RequestRevision, UserRoleClaims.FinancialExpert), LoanCaseStatus.RevisionRequestedByFinancial },

        { (LoanCaseStatus.PendingLegalFinalContract, LoanWorkflowAction.UploadFinalContract, UserRoleClaims.LegalExpert), LoanCaseStatus.PendingCeoFinalApproval },

        { (LoanCaseStatus.PendingCeoFinalApproval, LoanWorkflowAction.Approve, UserRoleClaims.Ceo), LoanCaseStatus.ReadyForPayment },
        { (LoanCaseStatus.PendingCeoFinalApproval, LoanWorkflowAction.Reject, UserRoleClaims.Ceo), LoanCaseStatus.CanceledByCeo },

        { (LoanCaseStatus.ReadyForPayment, LoanWorkflowAction.RegisterPayment, UserRoleClaims.FinancialExpert), LoanCaseStatus.RepaymentPhase },

        { (LoanCaseStatus.RepaymentPhase, LoanWorkflowAction.Approve, UserRoleClaims.Applicant), LoanCaseStatus.Completed }
    };

    static LoanCaseStateManager()
    {
        WorkflowRoleExpander.MirrorUnitManager(Transitions, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager);
        WorkflowRoleExpander.MirrorUnitManager(Transitions, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager);
        WorkflowRoleExpander.MirrorUnitManager(Transitions, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager);
    }

    #endregion

    #region State Machine

    public bool IsTerminalState(LoanCaseStatus status) => TerminalStates.Contains(status);

    public bool CanTransition(
        LoanCaseStatus currentStatus,
        LoanWorkflowAction action,
        string userRole,
        out LoanCaseStatus nextStatus,
        out string errorMessage)
    {
        nextStatus = currentStatus;

        if (IsTerminalState(currentStatus))
        {
            errorMessage = ApiMessages.CannotTransitionFromTerminalState;
            return false;
        }

        if (action == LoanWorkflowAction.Archive)
        {
            if (!string.Equals(userRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = ApiMessages.OnlyAdminCanArchive;
                return false;
            }

            nextStatus = LoanCaseStatus.Archived;
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
        LoanCase caseEntity,
        LoanWorkflowAction action,
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

        if (action == LoanWorkflowAction.Archive && caseEntity.CurrentStatus == LoanCaseStatus.Archived)
            return Task.FromResult(Result.Ok());

        if (!CanTransition(caseEntity.CurrentStatus, action, actorRole, out var nextStatus, out var errorMessage))
        {
            if (string.Equals(errorMessage, ApiMessages.OnlyAdminCanArchive, StringComparison.Ordinal))
                return Task.FromResult(Result.Fail(Error.Forbidden(errorMessage)));

            return Task.FromResult(Result.Fail(Error.Conflict(errorMessage)));
        }

        if (nextStatus == caseEntity.CurrentStatus)
            return Task.FromResult(Result.Ok());

        if (!ValidateBusinessRules(caseEntity, action, nextStatus, out var businessError))
            return Task.FromResult(Result.Fail(Error.Conflict(businessError)));

        if (action == LoanWorkflowAction.RequestRevision)
        {
            caseEntity.RequestRevision(nextStatus, actorId, actorRole, action, correlationId.Value, comment ?? string.Empty, isInternal: false);
            return Task.FromResult(Result.Ok());
        }

        caseEntity.TransitionTo(nextStatus, actorId, actorRole, action, correlationId.Value, comment);
        return Task.FromResult(Result.Ok());
    }

    private static bool ValidateBusinessRules(
        LoanCase caseEntity,
        LoanWorkflowAction action,
        LoanCaseStatus nextStatus,
        out string errorMessage)
    {
        switch (action)
        {
            case LoanWorkflowAction.Submit when caseEntity.CurrentStatus is LoanCaseStatus.DataEntry or LoanCaseStatus.RevisionRequestedByCredit:
                if (!LoanApplicationCompleteness.IsComplete(caseEntity.Application))
                {
                    errorMessage = ApiMessages.LoanApplicationIncomplete;
                    return false;
                }

                var missingDocs = LoanDocumentRequirements.GetMissingForDataEntrySubmit(caseEntity.Documents);
                if (missingDocs.Count > 0)
                {
                    errorMessage = LoanDocumentRequirements.FormatDataEntryDocumentsIncompleteMessage(missingDocs);
                    return false;
                }
                break;

            case LoanWorkflowAction.Approve when caseEntity.CurrentStatus == LoanCaseStatus.PendingCreditReview:
                if (!LoanApprovalDetailCompleteness.IsComplete(caseEntity.ApprovalDetail))
                {
                    errorMessage = ApiMessages.LoanApprovalDetailIncomplete;
                    return false;
                }
                break;

            case LoanWorkflowAction.SubmitInstallments:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == LoanDocumentType.RawContract))
                {
                    errorMessage = ApiMessages.LoanRawContractMissing;
                    return false;
                }

                if (caseEntity.Installments.Count == 0)
                {
                    errorMessage = ApiMessages.LoanInstallmentsMissing;
                    return false;
                }
                break;

            case LoanWorkflowAction.SubmitSignedPackage:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == LoanDocumentType.SignedContract))
                {
                    errorMessage = ApiMessages.LoanSignedContractMissing;
                    return false;
                }
                break;

            case LoanWorkflowAction.UploadFinalContract:
                if (!caseEntity.Documents.Any(x => !x.IsDeleted && x.DocumentType == LoanDocumentType.FinalContract))
                {
                    errorMessage = ApiMessages.LoanFinalContractMissing;
                    return false;
                }
                break;

            case LoanWorkflowAction.Approve when caseEntity.CurrentStatus == LoanCaseStatus.RepaymentPhase:
                if (caseEntity.Installments.Any(x => !x.IsPaid && !x.IsGracePeriod))
                {
                    errorMessage = ApiMessages.LoanRepaymentIncomplete;
                    return false;
                }
                break;
        }

        errorMessage = string.Empty;
        return true;
    }

    public IEnumerable<LoanWorkflowAction> GetAllowedActions(LoanCaseStatus currentStatus, string userRole)
    {
        if (IsTerminalState(currentStatus))
            return [];

        if (string.Equals(userRole, UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Transitions
                .Where(x => x.Key.Current == currentStatus)
                .Select(x => x.Key.Action)
                .Append(LoanWorkflowAction.Archive)
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
