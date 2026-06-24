namespace Core.Domain.Enums;

public enum GuaranteeCaseStatus
{
    Draft = 1,
    DataEntry = 2,
    CreditReview = 3,
    ApprovalFormEntry = 4,
    CeoApprovalInitial = 5,
    WaitingDraftContract = 6,
    WaitingSignedContractAndAttachments = 7,
    FinancialAttachmentReview = 8,
    WaitingFinalContract = 9,
    CeoApprovalFinal = 10,
    WaitingIssuanceDocuments = 11,
    Completed = 12,
    Rejected = 13,
    Cancelled = 14,
    Archived = 15,
    AmendmentDraft = 16,
    AmendmentDataEntry = 17,
    AmendmentCreditReview = 18,
    AmendmentCeoApproval = 19,
    AmendmentLegalReview = 20,
    AmendmentCompleted = 21,
    AmendmentApproved = 22,
    AmendmentRejected = 23
}
