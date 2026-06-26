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

public sealed class LoanCaseWorkflow : WorkflowBase
{
    public const string DefinitionId = $"{nameof(Core)}.{nameof(Workflow)}.{nameof(Workflows)}.{nameof(LoanCaseWorkflow)}";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId(DefinitionId);

        var caseId = GetCaseId(builder);
        var start = Stage("Start");
        var draft = Stage(nameof(LoanCaseStatus.Draft));
        var dataEntry = Stage(nameof(LoanCaseStatus.DataEntry));
        var creditReview = Stage(nameof(LoanCaseStatus.PendingCreditReview));
        var creditRevision = Stage(nameof(LoanCaseStatus.RevisionRequestedByCredit));
        var ceoInitial = Stage(nameof(LoanCaseStatus.PendingCeoInitialApproval));
        var legalRaw = Stage(nameof(LoanCaseStatus.PendingLegalRawContract));
        var applicantSignature = Stage(nameof(LoanCaseStatus.PendingApplicantSignature));
        var legalFinalReview = Stage(nameof(LoanCaseStatus.PendingLegalFinalReview));
        var legalRevision = Stage(nameof(LoanCaseStatus.RevisionRequestedByLegal));
        var financialReview = Stage(nameof(LoanCaseStatus.PendingFinancialReview));
        var financialRevision = Stage(nameof(LoanCaseStatus.RevisionRequestedByFinancial));
        var finalContract = Stage(nameof(LoanCaseStatus.PendingLegalFinalContract));
        var ceoFinal = Stage(nameof(LoanCaseStatus.PendingCeoFinalApproval));
        var payment = Stage(nameof(LoanCaseStatus.ReadyForPayment));
        var repayment = Stage(nameof(LoanCaseStatus.RepaymentPhase));
        var completed = Stage(nameof(LoanCaseStatus.Completed));
        var canceledByCeo = Stage(nameof(LoanCaseStatus.CanceledByCeo));
        var archived = Stage(nameof(LoanCaseStatus.Archived));

        var flow = new Flowchart();
        Add(flow, start, draft, dataEntry, creditReview, creditRevision, ceoInitial, legalRaw, applicantSignature,
            legalFinalReview, legalRevision, financialReview, financialRevision, finalContract, ceoFinal,
            payment, repayment, completed, canceledByCeo, archived);

        Link(flow, start, draft);
        Route(flow, draft, Command("DraftSubmit", caseId, LoanWorkflowAction.Submit, LoanCaseStatus.DataEntry, UserRoleClaims.Applicant), dataEntry);
        Route(flow, dataEntry, Command("SubmitDataEntry", caseId, LoanWorkflowAction.Submit, LoanCaseStatus.PendingCreditReview, UserRoleClaims.Applicant), creditReview);
        Route(flow, creditRevision, Command("SubmitCreditRevision", caseId, LoanWorkflowAction.Submit, LoanCaseStatus.PendingCreditReview, UserRoleClaims.Applicant), creditReview);

        Route(flow, creditReview, Command("CreditApprove", caseId, LoanWorkflowAction.Approve, LoanCaseStatus.PendingCeoInitialApproval, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), ceoInitial);
        Route(flow, creditReview, Command("CreditRevision", caseId, LoanWorkflowAction.RequestRevision, LoanCaseStatus.RevisionRequestedByCredit, UserRoleClaims.CreditExpert, UserRoleClaims.CreditManager), creditRevision);

        Route(flow, ceoInitial, Command("CeoInitialApprove", caseId, LoanWorkflowAction.Approve, LoanCaseStatus.PendingLegalRawContract, UserRoleClaims.Ceo), legalRaw);
        Route(flow, ceoInitial, Command("CeoInitialReject", caseId, LoanWorkflowAction.Reject, LoanCaseStatus.CanceledByCeo, UserRoleClaims.Ceo), canceledByCeo);

        Route(flow, legalRaw, Command("SubmitInstallments", caseId, LoanWorkflowAction.SubmitInstallments, LoanCaseStatus.PendingApplicantSignature, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), applicantSignature);

        Route(flow, applicantSignature, Command("SubmitSignedPackage", caseId, LoanWorkflowAction.SubmitSignedPackage, LoanCaseStatus.PendingLegalFinalReview, UserRoleClaims.Applicant), legalFinalReview);
        Route(flow, legalRevision, Command("SubmitLegalRevision", caseId, LoanWorkflowAction.SubmitSignedPackage, LoanCaseStatus.PendingLegalFinalReview, UserRoleClaims.Applicant), legalFinalReview);
        Route(flow, financialRevision, Command("SubmitFinancialRevision", caseId, LoanWorkflowAction.SubmitSignedPackage, LoanCaseStatus.PendingLegalFinalReview, UserRoleClaims.Applicant), legalFinalReview);

        Route(flow, legalFinalReview, Command("LegalFinalApprove", caseId, LoanWorkflowAction.Approve, LoanCaseStatus.PendingFinancialReview, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), financialReview);
        Route(flow, legalFinalReview, Command("LegalFinalRevision", caseId, LoanWorkflowAction.RequestRevision, LoanCaseStatus.RevisionRequestedByLegal, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), legalRevision);

        Route(flow, financialReview, Command("FinancialApprove", caseId, LoanWorkflowAction.Approve, LoanCaseStatus.PendingLegalFinalContract, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), finalContract);
        Route(flow, financialReview, Command("FinancialRevision", caseId, LoanWorkflowAction.RequestRevision, LoanCaseStatus.RevisionRequestedByFinancial, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), financialRevision);

        Route(flow, finalContract, Command("UploadFinalContract", caseId, LoanWorkflowAction.UploadFinalContract, LoanCaseStatus.PendingCeoFinalApproval, UserRoleClaims.LegalExpert, UserRoleClaims.LegalManager), ceoFinal);

        Route(flow, ceoFinal, Command("CeoFinalApprove", caseId, LoanWorkflowAction.Approve, LoanCaseStatus.ReadyForPayment, UserRoleClaims.Ceo), payment);
        Route(flow, ceoFinal, Command("CeoFinalReject", caseId, LoanWorkflowAction.Reject, LoanCaseStatus.CanceledByCeo, UserRoleClaims.Ceo), canceledByCeo);

        Route(flow, payment, Command("RegisterPayment", caseId, LoanWorkflowAction.RegisterPayment, LoanCaseStatus.RepaymentPhase, UserRoleClaims.FinancialExpert, UserRoleClaims.FinancialManager), repayment);
        Route(flow, repayment, Command("CompleteRepayment", caseId, LoanWorkflowAction.Approve, LoanCaseStatus.Completed, UserRoleClaims.Applicant), completed);

        AddArchiveRoute(flow, draft, "Draft", caseId, archived);
        AddArchiveRoute(flow, dataEntry, "DataEntry", caseId, archived);
        AddArchiveRoute(flow, creditReview, "CreditReview", caseId, archived);
        AddArchiveRoute(flow, creditRevision, "CreditRevision", caseId, archived);
        AddArchiveRoute(flow, ceoInitial, "CeoInitial", caseId, archived);
        AddArchiveRoute(flow, legalRaw, "LegalRaw", caseId, archived);
        AddArchiveRoute(flow, applicantSignature, "ApplicantSignature", caseId, archived);
        AddArchiveRoute(flow, legalFinalReview, "LegalFinalReview", caseId, archived);
        AddArchiveRoute(flow, legalRevision, "LegalRevision", caseId, archived);
        AddArchiveRoute(flow, financialReview, "FinancialReview", caseId, archived);
        AddArchiveRoute(flow, financialRevision, "FinancialRevision", caseId, archived);
        AddArchiveRoute(flow, finalContract, "FinalContract", caseId, archived);
        AddArchiveRoute(flow, ceoFinal, "CeoFinal", caseId, archived);
        AddArchiveRoute(flow, payment, "Payment", caseId, archived);
        AddArchiveRoute(flow, repayment, "Repayment", caseId, archived);

        builder.Root = flow;
    }

    private static WriteLine Stage(string id) => new($"Loan {id}") { Id = id };

    private static WaitForCaseCommandActivity Command(string id, Input<Guid> caseId, LoanWorkflowAction action, LoanCaseStatus target, params string[] roles) => new()
    {
        Id = id,
        CaseId = caseId,
        CommandName = new(action.ToString()),
        TargetStatus = new(target.ToString()),
        AllowedRoles = new(string.Join(';', roles))
    };

    private static void AddArchiveRoute(Flowchart flow, IActivity source, string sourceId, Input<Guid> caseId, IActivity archived)
        => Route(flow, source, Command($"{sourceId}Archive", caseId, LoanWorkflowAction.Archive, LoanCaseStatus.Archived, UserRoleClaims.Admin), archived);

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
