using Core.Application.Kanban;
using Core.Domain.Enums;

namespace Core.Application.Common;

public static class CaseStageOrder
{
    private static readonly Dictionary<int, int> InvestmentStatusRanks = new()
    {
        [(int)CaseStatus.Draft] = 1,
        [(int)CaseStatus.DataEntry1] = 2,
        [(int)CaseStatus.ReviewDataEntry1] = 3,
        [(int)CaseStatus.DataEntry2] = 4,
        [(int)CaseStatus.ReviewDataEntry2] = 5,
        [(int)CaseStatus.InitialValuation] = 6,
        [(int)CaseStatus.SecondaryValuation] = 7,
        [(int)CaseStatus.WaitingPreliminaryContract] = 8,
        [(int)CaseStatus.WaitingUserReviewPreliminaryContract] = 9,
        [(int)CaseStatus.ContractDrafting] = 10,
        [(int)CaseStatus.WaitingContractSignature] = 11,
        [(int)CaseStatus.WaitingSignedContractUpload] = 12,
        [(int)CaseStatus.WaitingFinancialWorksheet] = 13,
        [(int)CaseStatus.FinancialWorksheetReview] = 14,
        [(int)CaseStatus.WaitingCeoApproval] = 15,
        [(int)CaseStatus.WaitingPayment] = 16,
        [(int)CaseStatus.Completed] = 17,
        [(int)CaseStatus.Rejected] = 18,
        [(int)CaseStatus.Cancelled] = 19,
        [(int)CaseStatus.Archived] = 20
    };

    private static readonly Dictionary<int, int> GuaranteeStatusRanks = new()
    {
        [(int)GuaranteeCaseStatus.Draft] = 1,
        [(int)GuaranteeCaseStatus.DataEntry] = 2,
        [(int)GuaranteeCaseStatus.CreditReview] = 3,
        [(int)GuaranteeCaseStatus.ApprovalFormEntry] = 4,
        [(int)GuaranteeCaseStatus.CeoApprovalInitial] = 5,
        [(int)GuaranteeCaseStatus.WaitingDraftContract] = 6,
        [(int)GuaranteeCaseStatus.WaitingSignedContractAndAttachments] = 7,
        [(int)GuaranteeCaseStatus.FinancialAttachmentReview] = 8,
        [(int)GuaranteeCaseStatus.WaitingFinalContract] = 9,
        [(int)GuaranteeCaseStatus.CeoApprovalFinal] = 10,
        [(int)GuaranteeCaseStatus.WaitingIssuanceDocuments] = 11,
        [(int)GuaranteeCaseStatus.Completed] = 12,
        [(int)GuaranteeCaseStatus.AmendmentDraft] = 13,
        [(int)GuaranteeCaseStatus.AmendmentDataEntry] = 14,
        [(int)GuaranteeCaseStatus.AmendmentCreditReview] = 15,
        [(int)GuaranteeCaseStatus.AmendmentCeoApproval] = 16,
        [(int)GuaranteeCaseStatus.AmendmentLegalReview] = 17,
        [(int)GuaranteeCaseStatus.AmendmentApproved] = 18,
        [(int)GuaranteeCaseStatus.AmendmentRejected] = 19,
        [(int)GuaranteeCaseStatus.Rejected] = 90,
        [(int)GuaranteeCaseStatus.Cancelled] = 91,
        [(int)GuaranteeCaseStatus.Archived] = 92
    };

    public static bool TryGetRank(CaseModuleType module, int statusValue, out int rank)
    {
        rank = 0;

        return module switch
        {
            CaseModuleType.Investment => InvestmentStatusRanks.TryGetValue(statusValue, out rank),
            CaseModuleType.Guarantee => TryGetGuaranteeRank(statusValue, out rank),
            CaseModuleType.Loan => TryGetLoanRank(statusValue, out rank),
            _ => false
        };
    }

    public static string GetTitle(CaseModuleType module, int statusValue)
    {
        return module switch
        {
            CaseModuleType.Investment when Enum.IsDefined(typeof(CaseStatus), statusValue)
                => CaseKanbanRules.GetStatusTitle((CaseStatus)statusValue),
            CaseModuleType.Guarantee when Enum.IsDefined(typeof(GuaranteeCaseStatus), statusValue)
                => GuaranteeKanbanRules.GetStatusTitle((GuaranteeCaseStatus)statusValue),
            CaseModuleType.Loan when Enum.IsDefined(typeof(LoanCaseStatus), statusValue)
                => LoanKanbanRules.GetStatusTitle((LoanCaseStatus)statusValue),
            _ => statusValue.ToString()
        };
    }

    private static bool TryGetGuaranteeRank(int statusValue, out int rank)
    {
        rank = 0;
        return GuaranteeStatusRanks.TryGetValue(statusValue, out rank);
    }

    private static bool TryGetLoanRank(int statusValue, out int rank)
    {
        rank = 0;
        if (!Enum.IsDefined(typeof(LoanCaseStatus), statusValue))
            return false;

        rank = statusValue;
        return true;
    }
}
