namespace Core.Domain.Enums;

public enum GuaranteeWorkflowAction
{
    Submit = 1,
    Approve = 2,
    RequestRevision = 3,
    Reject = 4,
    Cancel = 5,
    UploadDraftContract = 6,
    SubmitSignedPackage = 7,
    ApproveAttachments = 8,
    UploadFinalContract = 9,
    UploadIssuanceDocuments = 10,
    Archive = 11,
    BeginAmendment = 12,
    StageRollback = 13
}
