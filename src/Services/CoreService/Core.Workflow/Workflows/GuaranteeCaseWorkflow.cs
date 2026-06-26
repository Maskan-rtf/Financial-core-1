using Core.Domain.Enums;
using Core.Domain.Identity;
using Core.Workflow.Activities;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Models;
using Elsa.Workflows.Models;

namespace Core.Workflow.Workflows;

public sealed class GuaranteeCaseWorkflow : WorkflowBase
{
    public const string DefinitionId = $"{nameof(Core)}.{nameof(Workflow)}.{nameof(Workflows)}.{nameof(GuaranteeCaseWorkflow)}";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId(DefinitionId);

        var caseId = GetCaseId(builder);
        var start = Stage("Start");
        var draft = Stage(nameof(GuaranteeCaseStatus.Draft));
        var dataEntry = Stage(nameof(GuaranteeCaseStatus.DataEntry));
        var creditReview = Stage(nameof(GuaranteeCaseStatus.CreditReview));
        var approvalForm = Stage(nameof(GuaranteeCaseStatus.ApprovalFormEntry));
        var ceoInitial = Stage(nameof(GuaranteeCaseStatus.CeoApprovalInitial));
        var draftContract = Stage(nameof(GuaranteeCaseStatus.WaitingDraftContract));
        var signedPackage = Stage(nameof(GuaranteeCaseStatus.WaitingSignedContractAndAttachments));
        var attachmentReview = Stage(nameof(GuaranteeCaseStatus.FinancialAttachmentReview));
        var finalContract = Stage(nameof(GuaranteeCaseStatus.WaitingFinalContract));
        var ceoFinal = Stage(nameof(GuaranteeCaseStatus.CeoApprovalFinal));
        var issuance = Stage(nameof(GuaranteeCaseStatus.WaitingIssuanceDocuments));
        var completed = Stage(nameof(GuaranteeCaseStatus.Completed));
        var amendmentDraft = Stage(nameof(GuaranteeCaseStatus.AmendmentDraft));
        var amendmentDataEntry = Stage(nameof(GuaranteeCaseStatus.AmendmentDataEntry));
        var amendmentCredit = Stage(nameof(GuaranteeCaseStatus.AmendmentCreditReview));
        var amendmentCeo = Stage(nameof(GuaranteeCaseStatus.AmendmentCeoApproval));
        var amendmentLegal = Stage(nameof(GuaranteeCaseStatus.AmendmentLegalReview));
        var amendmentApproved = Stage(nameof(GuaranteeCaseStatus.AmendmentApproved));
        var amendmentRejected = Stage(nameof(GuaranteeCaseStatus.AmendmentRejected));
        var cancelled = Stage(nameof(GuaranteeCaseStatus.Cancelled));
        var rejected = Stage(nameof(GuaranteeCaseStatus.Rejected));
        var archived = Stage(nameof(GuaranteeCaseStatus.Archived));

        var flow = new Flowchart();
        Add(flow, start, draft, dataEntry, creditReview, approvalForm, ceoInitial, draftContract, signedPackage,
            attachmentReview, finalContract, ceoFinal, issuance, completed, amendmentDraft, amendmentDataEntry,
            amendmentCredit, amendmentCeo, amendmentLegal, amendmentApproved, amendmentRejected, cancelled,
            rejected, archived);

        Link(flow, start, draft);
        Route(flow, draft, Command("DraftSubmit", caseId, GuaranteeWorkflowAction.Submit, GuaranteeCaseStatus.DataEntry, UserRoleClaims.Applicant), dataEntry);
        Route(flow, dataEntry, Command("SubmitDataEntry", caseId, GuaranteeWorkflowAction.Submit, GuaranteeCaseStatus.CreditReview, UserRoleClaims.Applicant), creditReview);
        Route(flow, dataEntry, Command("CancelDataEntry", caseId, GuaranteeWorkflowAction.Cancel, GuaranteeCaseStatus.Cancelled, UserRoleClaims.Applicant), cancelled);

        Route(flow, creditReview, Command("CreditApprove", caseId, GuaranteeWorkflowAction.Approve, GuaranteeCaseStatus.ApprovalFormEntry, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), approvalForm);
        Route(flow, creditReview, Command("CreditRevision", caseId, GuaranteeWorkflowAction.RequestRevision, GuaranteeCaseStatus.DataEntry, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), dataEntry);
        Route(flow, creditReview, Command("CreditReject", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.Rejected, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), rejected);

        Route(flow, approvalForm, Command("SubmitApprovalForm", caseId, GuaranteeWorkflowAction.Submit, GuaranteeCaseStatus.CeoApprovalInitial, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), ceoInitial);
        Route(flow, approvalForm, Command("RejectApprovalForm", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.Rejected, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), rejected);

        Route(flow, ceoInitial, Command("CeoInitialApprove", caseId, GuaranteeWorkflowAction.Approve, GuaranteeCaseStatus.WaitingDraftContract, UserRoleClaims.Ceo), draftContract);
        Route(flow, ceoInitial, Command("CeoInitialReject", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.Rejected, UserRoleClaims.Ceo), rejected);
        Route(flow, ceoInitial, Command("CeoInitialCancel", caseId, GuaranteeWorkflowAction.Cancel, GuaranteeCaseStatus.Cancelled, UserRoleClaims.Ceo), cancelled);

        Route(flow, draftContract, Command("UploadDraftContract", caseId, GuaranteeWorkflowAction.UploadDraftContract, GuaranteeCaseStatus.WaitingSignedContractAndAttachments, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), signedPackage);
        Route(flow, draftContract, Command("RejectDraftContract", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.Rejected, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), rejected);

        Route(flow, signedPackage, Command("SubmitSignedPackage", caseId, GuaranteeWorkflowAction.SubmitSignedPackage, GuaranteeCaseStatus.FinancialAttachmentReview, UserRoleClaims.Applicant), attachmentReview);
        Route(flow, signedPackage, Command("CancelSignedPackage", caseId, GuaranteeWorkflowAction.Cancel, GuaranteeCaseStatus.Cancelled, UserRoleClaims.Applicant), cancelled);

        Route(flow, attachmentReview, Command("ApproveAttachments", caseId, GuaranteeWorkflowAction.ApproveAttachments, GuaranteeCaseStatus.WaitingFinalContract, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), finalContract);
        Route(flow, attachmentReview, Command("ReviseAttachments", caseId, GuaranteeWorkflowAction.RequestRevision, GuaranteeCaseStatus.WaitingSignedContractAndAttachments, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), signedPackage);
        Route(flow, attachmentReview, Command("RejectAttachments", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.Rejected, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), rejected);

        Route(flow, finalContract, Command("UploadFinalContract", caseId, GuaranteeWorkflowAction.UploadFinalContract, GuaranteeCaseStatus.CeoApprovalFinal, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), ceoFinal);
        Route(flow, finalContract, Command("RejectFinalContract", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.Rejected, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), rejected);

        Route(flow, ceoFinal, Command("CeoFinalApprove", caseId, GuaranteeWorkflowAction.Approve, GuaranteeCaseStatus.WaitingIssuanceDocuments, UserRoleClaims.Ceo), issuance);
        Route(flow, ceoFinal, Command("CeoFinalReject", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.Rejected, UserRoleClaims.Ceo), rejected);
        Route(flow, ceoFinal, Command("CeoFinalCancel", caseId, GuaranteeWorkflowAction.Cancel, GuaranteeCaseStatus.Cancelled, UserRoleClaims.Ceo), cancelled);

        Route(flow, issuance, Command("UploadIssuanceDocuments", caseId, GuaranteeWorkflowAction.UploadIssuanceDocuments, GuaranteeCaseStatus.Completed, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), completed);
        Route(flow, issuance, Command("RejectIssuance", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.Rejected, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), rejected);

        Route(flow, completed, Command("BeginAmendmentFromCompleted", caseId, GuaranteeWorkflowAction.BeginAmendment, GuaranteeCaseStatus.AmendmentDraft, UserRoleClaims.Applicant), amendmentDraft);
        Route(flow, amendmentApproved, Command("BeginAmendmentFromApproved", caseId, GuaranteeWorkflowAction.BeginAmendment, GuaranteeCaseStatus.AmendmentDraft, UserRoleClaims.Applicant), amendmentDraft);
        Route(flow, amendmentRejected, Command("BeginAmendmentFromRejected", caseId, GuaranteeWorkflowAction.BeginAmendment, GuaranteeCaseStatus.AmendmentDraft, UserRoleClaims.Applicant), amendmentDraft);

        Route(flow, amendmentDraft, Command("SubmitAmendmentDraft", caseId, GuaranteeWorkflowAction.Submit, GuaranteeCaseStatus.AmendmentDataEntry, UserRoleClaims.Applicant), amendmentDataEntry);
        Route(flow, amendmentDataEntry, Command("SubmitAmendmentDataEntry", caseId, GuaranteeWorkflowAction.Submit, GuaranteeCaseStatus.AmendmentCreditReview, UserRoleClaims.Applicant), amendmentCredit);
        Route(flow, amendmentDataEntry, Command("SubmitAmendmentDataEntryNoCreditReview", caseId, GuaranteeWorkflowAction.Submit, GuaranteeCaseStatus.AmendmentCeoApproval, UserRoleClaims.Applicant), amendmentCeo);
        Route(flow, amendmentDataEntry, Command("CancelAmendmentDataEntry", caseId, GuaranteeWorkflowAction.Cancel, GuaranteeCaseStatus.AmendmentRejected, UserRoleClaims.Applicant), amendmentRejected);

        Route(flow, amendmentCredit, Command("AmendmentCreditApprove", caseId, GuaranteeWorkflowAction.Approve, GuaranteeCaseStatus.AmendmentCeoApproval, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), amendmentCeo);
        Route(flow, amendmentCredit, Command("AmendmentCreditRevision", caseId, GuaranteeWorkflowAction.RequestRevision, GuaranteeCaseStatus.AmendmentDataEntry, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), amendmentDataEntry);
        Route(flow, amendmentCredit, Command("AmendmentCreditReject", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.AmendmentRejected, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), amendmentRejected);

        Route(flow, amendmentCeo, Command("AmendmentCeoApprove", caseId, GuaranteeWorkflowAction.Approve, GuaranteeCaseStatus.AmendmentLegalReview, UserRoleClaims.Ceo), amendmentLegal);
        Route(flow, amendmentCeo, Command("AmendmentCeoReject", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.AmendmentRejected, UserRoleClaims.Ceo), amendmentRejected);

        Route(flow, amendmentLegal, Command("AmendmentLegalApprove", caseId, GuaranteeWorkflowAction.Approve, GuaranteeCaseStatus.AmendmentApproved, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), amendmentApproved);
        Route(flow, amendmentLegal, Command("AmendmentLegalRevision", caseId, GuaranteeWorkflowAction.RequestRevision, GuaranteeCaseStatus.AmendmentDataEntry, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), amendmentDataEntry);
        Route(flow, amendmentLegal, Command("AmendmentLegalReject", caseId, GuaranteeWorkflowAction.Reject, GuaranteeCaseStatus.AmendmentRejected, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), amendmentRejected);

        AddArchiveRoute(flow, draft, "Draft", caseId, archived);
        AddArchiveRoute(flow, dataEntry, "DataEntry", caseId, archived);
        AddArchiveRoute(flow, creditReview, "CreditReview", caseId, archived);
        AddArchiveRoute(flow, approvalForm, "ApprovalForm", caseId, archived);
        AddArchiveRoute(flow, ceoInitial, "CeoInitial", caseId, archived);
        AddArchiveRoute(flow, draftContract, "DraftContract", caseId, archived);
        AddArchiveRoute(flow, signedPackage, "SignedPackage", caseId, archived);
        AddArchiveRoute(flow, attachmentReview, "AttachmentReview", caseId, archived);
        AddArchiveRoute(flow, finalContract, "FinalContract", caseId, archived);
        AddArchiveRoute(flow, ceoFinal, "CeoFinal", caseId, archived);
        AddArchiveRoute(flow, issuance, "Issuance", caseId, archived);
        AddArchiveRoute(flow, completed, "Completed", caseId, archived);
        AddArchiveRoute(flow, amendmentDraft, "AmendmentDraft", caseId, archived);
        AddArchiveRoute(flow, amendmentDataEntry, "AmendmentDataEntry", caseId, archived);
        AddArchiveRoute(flow, amendmentCredit, "AmendmentCredit", caseId, archived);
        AddArchiveRoute(flow, amendmentCeo, "AmendmentCeo", caseId, archived);
        AddArchiveRoute(flow, amendmentLegal, "AmendmentLegal", caseId, archived);
        AddArchiveRoute(flow, amendmentApproved, "AmendmentApproved", caseId, archived);
        AddArchiveRoute(flow, amendmentRejected, "AmendmentRejected", caseId, archived);

        builder.Root = flow;
    }

    private static WriteLine Stage(string id) => new($"Guarantee {id}") { Id = id };

    private static WaitForCaseCommandActivity Command(string id, Input<Guid> caseId, GuaranteeWorkflowAction action, GuaranteeCaseStatus target, params string[] roles) => new()
    {
        Id = id,
        CaseId = caseId,
        CommandName = new(action.ToString()),
        TargetStatus = new(target.ToString()),
        AllowedRoles = new(string.Join(';', roles))
    };

    private static void AddArchiveRoute(Flowchart flow, IActivity source, string sourceId, Input<Guid> caseId, IActivity archived)
        => Route(flow, source, Command($"{sourceId}Archive", caseId, GuaranteeWorkflowAction.Archive, GuaranteeCaseStatus.Archived, UserRoleClaims.Admin), archived);

    private static void Route(Flowchart flow, IActivity source, WaitForCaseCommandActivity command, IActivity target)
    {
        Add(flow, command);
        Link(flow, source, command);
        Link(flow, command, target);
    }

    private static void Add(Flowchart flow, params IActivity[] activities)
    {
        foreach (var activity in activities)
            flow.Activities.Add(activity);
    }

    private static void Link(Flowchart flow, IActivity source, IActivity target)
        => flow.Connections.Add(new Connection(source, target));

    private static Input<Guid> GetCaseId(IWorkflowBuilder builder) => new(ctx => ctx.GetInput<Guid>("CaseId"));
}
