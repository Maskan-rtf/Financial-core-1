namespace Core.Domain.Enums;

public enum WorkflowAction
{
    Submit = 1,
    Approve = 2,
    RequestRevision = 3,
    UploadPreliminaryContract = 4,
    FinalizeContractDraft = 5,
    ConfirmSignature = 6,
    UploadSignedContract = 7,
    SubmitFinancialWorksheet = 8,
    ApproveFinancialWorksheet = 9,
    CompletePayment = 10,
    Reject = 11,
    Cancel = 12,
    Archive = 13,
    StageRollback = 14
}
