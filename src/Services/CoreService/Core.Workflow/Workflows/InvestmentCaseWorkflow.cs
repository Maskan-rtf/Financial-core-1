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

public sealed class InvestmentCaseWorkflow : WorkflowBase
{
    public const string DefinitionId = $"{nameof(Core)}.{nameof(Workflow)}.{nameof(Workflows)}.{nameof(InvestmentCaseWorkflow)}";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId(DefinitionId);

        var caseId = GetCaseId(builder);
        var start = Stage("Start");
        var draft = Stage(nameof(CaseStatus.Draft));
        var dataEntry1 = Stage(nameof(CaseStatus.DataEntry1));
        var review1 = Stage(nameof(CaseStatus.ReviewDataEntry1));
        var dataEntry2 = Stage(nameof(CaseStatus.DataEntry2));
        var review2 = Stage(nameof(CaseStatus.ReviewDataEntry2));
        var initialValuation = Stage(nameof(CaseStatus.InitialValuation));
        var secondaryValuation = Stage(nameof(CaseStatus.SecondaryValuation));
        var preliminaryContract = Stage(nameof(CaseStatus.WaitingPreliminaryContract));
        var userPreliminaryReview = Stage(nameof(CaseStatus.WaitingUserReviewPreliminaryContract));
        var drafting = Stage(nameof(CaseStatus.ContractDrafting));
        var signature = Stage(nameof(CaseStatus.WaitingContractSignature));
        var signedUpload = Stage(nameof(CaseStatus.WaitingSignedContractUpload));
        var worksheet = Stage(nameof(CaseStatus.WaitingFinancialWorksheet));
        var worksheetReview = Stage(nameof(CaseStatus.FinancialWorksheetReview));
        var ceoApproval = Stage(nameof(CaseStatus.WaitingCeoApproval));
        var payment = Stage(nameof(CaseStatus.WaitingPayment));
        var completed = Stage(nameof(CaseStatus.Completed));
        var cancelled = Stage(nameof(CaseStatus.Cancelled));
        var rejected = Stage(nameof(CaseStatus.Rejected));
        var archived = Stage(nameof(CaseStatus.Archived));

        var flow = new Flowchart();
        Add(flow, start, draft, dataEntry1, review1, dataEntry2, review2, initialValuation, secondaryValuation,
            preliminaryContract, userPreliminaryReview, drafting, signature, signedUpload, worksheet,
            worksheetReview, ceoApproval, payment, completed, cancelled, rejected, archived);

        Link(flow, start, draft);
        Route(flow, draft, Command("DraftSubmit", caseId, WorkflowAction.Submit, CaseStatus.DataEntry1, UserRoleClaims.Applicant), dataEntry1);
        Route(flow, draft, Command("DraftCancel", caseId, WorkflowAction.Cancel, CaseStatus.Cancelled, UserRoleClaims.Applicant), cancelled);
        Route(flow, dataEntry1, Command("DataEntry1Submit", caseId, WorkflowAction.Submit, CaseStatus.ReviewDataEntry1, UserRoleClaims.Applicant), review1);
        Route(flow, dataEntry1, Command("DataEntry1Cancel", caseId, WorkflowAction.Cancel, CaseStatus.Cancelled, UserRoleClaims.Applicant), cancelled);

        Route(flow, review1, Command("Review1Approve", caseId, WorkflowAction.Approve, CaseStatus.DataEntry2, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager), dataEntry2);
        Route(flow, review1, Command("Review1Revision", caseId, WorkflowAction.RequestRevision, CaseStatus.DataEntry1, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager), dataEntry1);
        Route(flow, review1, Command("Review1Reject", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager), rejected);

        Route(flow, dataEntry2, Command("DataEntry2Submit", caseId, WorkflowAction.Submit, CaseStatus.ReviewDataEntry2, UserRoleClaims.Applicant), review2);
        Route(flow, dataEntry2, Command("DataEntry2Cancel", caseId, WorkflowAction.Cancel, CaseStatus.Cancelled, UserRoleClaims.Applicant), cancelled);

        Route(flow, review2, Command("Review2Approve", caseId, WorkflowAction.Approve, CaseStatus.InitialValuation, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager), initialValuation);
        Route(flow, review2, Command("Review2Revision", caseId, WorkflowAction.RequestRevision, CaseStatus.DataEntry2, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager), dataEntry2);
        Route(flow, review2, Command("Review2Reject", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager), rejected);

        Route(flow, initialValuation, Command("InitialValuationApprove", caseId, WorkflowAction.Approve, CaseStatus.SecondaryValuation, UserRoleClaims.InvestmentManager), secondaryValuation);
        Route(flow, initialValuation, Command("InitialValuationReject", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.InvestmentManager), rejected);

        Route(flow, secondaryValuation, Command("SecondaryValuationApprove", caseId, WorkflowAction.Approve, CaseStatus.WaitingPreliminaryContract, UserRoleClaims.InvestmentManager), preliminaryContract);
        Route(flow, secondaryValuation, Command("SecondaryValuationReject", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.InvestmentManager), rejected);

        Route(flow, preliminaryContract, Command("UploadPreliminaryContract", caseId, WorkflowAction.UploadPreliminaryContract, CaseStatus.WaitingUserReviewPreliminaryContract, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), userPreliminaryReview);
        Route(flow, preliminaryContract, Command("RejectPreliminaryContract", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), rejected);

        Route(flow, userPreliminaryReview, Command("PreliminaryUserApprove", caseId, WorkflowAction.Approve, CaseStatus.ContractDrafting, UserRoleClaims.Applicant), drafting);
        Route(flow, userPreliminaryReview, Command("PreliminaryUserRevision", caseId, WorkflowAction.RequestRevision, CaseStatus.WaitingPreliminaryContract, UserRoleClaims.Applicant), preliminaryContract);
        Route(flow, userPreliminaryReview, Command("PreliminaryUserCancel", caseId, WorkflowAction.Cancel, CaseStatus.Cancelled, UserRoleClaims.Applicant), cancelled);

        Route(flow, drafting, Command("FinalizeContractDraft", caseId, WorkflowAction.FinalizeContractDraft, CaseStatus.WaitingContractSignature, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), signature);
        Route(flow, drafting, Command("RejectContractDraft", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), rejected);

        Route(flow, signature, Command("ConfirmSignature", caseId, WorkflowAction.ConfirmSignature, CaseStatus.WaitingSignedContractUpload, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), signedUpload);
        Route(flow, signature, Command("RejectSignature", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), rejected);

        Route(flow, signedUpload, Command("UploadSignedContract", caseId, WorkflowAction.UploadSignedContract, CaseStatus.WaitingFinancialWorksheet, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), worksheet);
        Route(flow, signedUpload, Command("RejectSignedContract", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), rejected);

        Route(flow, worksheet, Command("SubmitFinancialWorksheet", caseId, WorkflowAction.SubmitFinancialWorksheet, CaseStatus.FinancialWorksheetReview, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager), worksheetReview);
        Route(flow, worksheet, Command("RejectFinancialWorksheetEntry", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.InvestmentExpert, UserRoleClaims.InvestmentManager), rejected);

        Route(flow, worksheetReview, Command("ApproveFinancialWorksheet", caseId, WorkflowAction.ApproveFinancialWorksheet, CaseStatus.WaitingCeoApproval, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), ceoApproval);
        Route(flow, worksheetReview, Command("ReviseFinancialWorksheet", caseId, WorkflowAction.RequestRevision, CaseStatus.WaitingFinancialWorksheet, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), worksheet);
        Route(flow, worksheetReview, Command("RejectFinancialWorksheetReview", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), rejected);

        Route(flow, ceoApproval, Command("CeoApprove", caseId, WorkflowAction.Approve, CaseStatus.WaitingPayment, UserRoleClaims.Ceo), payment);
        Route(flow, ceoApproval, Command("CeoRevision", caseId, WorkflowAction.RequestRevision, CaseStatus.WaitingFinancialWorksheet, UserRoleClaims.Ceo), worksheet);
        Route(flow, ceoApproval, Command("CeoReject", caseId, WorkflowAction.Reject, CaseStatus.Rejected, UserRoleClaims.Ceo), rejected);

        Route(flow, payment, Command("CompletePayment", caseId, WorkflowAction.CompletePayment, CaseStatus.Completed, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), completed);
        Route(flow, payment, Command("CancelPayment", caseId, WorkflowAction.Cancel, CaseStatus.Cancelled, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), cancelled);

        AddArchiveRoute(flow, draft, "Draft", caseId, archived);
        AddArchiveRoute(flow, dataEntry1, "DataEntry1", caseId, archived);
        AddArchiveRoute(flow, review1, "Review1", caseId, archived);
        AddArchiveRoute(flow, dataEntry2, "DataEntry2", caseId, archived);
        AddArchiveRoute(flow, review2, "Review2", caseId, archived);
        AddArchiveRoute(flow, initialValuation, "InitialValuation", caseId, archived);
        AddArchiveRoute(flow, secondaryValuation, "SecondaryValuation", caseId, archived);
        AddArchiveRoute(flow, preliminaryContract, "PreliminaryContract", caseId, archived);
        AddArchiveRoute(flow, userPreliminaryReview, "UserPreliminaryReview", caseId, archived);
        AddArchiveRoute(flow, drafting, "Drafting", caseId, archived);
        AddArchiveRoute(flow, signature, "Signature", caseId, archived);
        AddArchiveRoute(flow, signedUpload, "SignedUpload", caseId, archived);
        AddArchiveRoute(flow, worksheet, "Worksheet", caseId, archived);
        AddArchiveRoute(flow, worksheetReview, "WorksheetReview", caseId, archived);
        AddArchiveRoute(flow, ceoApproval, "CeoApproval", caseId, archived);
        AddArchiveRoute(flow, payment, "Payment", caseId, archived);

        builder.Root = flow;
    }

    private static WriteLine Stage(string id) => new($"Investment {id}") { Id = id };

    private static WaitForCaseCommandActivity Command(string id, Input<Guid> caseId, WorkflowAction action, CaseStatus target, params string[] roles) => new()
    {
        Id = id,
        CaseId = caseId,
        CommandName = new(action.ToString()),
        TargetStatus = new(target.ToString()),
        AllowedRoles = new(string.Join(';', roles))
    };

    private static void AddArchiveRoute(Flowchart flow, IActivity source, string sourceId, Input<Guid> caseId, IActivity archived)
        => Route(flow, source, Command($"{sourceId}Archive", caseId, WorkflowAction.Archive, CaseStatus.Archived, UserRoleClaims.Admin), archived);

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
