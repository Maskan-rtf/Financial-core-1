namespace Core.Domain.Enums;

public enum LoanWorkflowAction
{
    Submit = 1,
    Approve = 2,
    RequestRevision = 3,
    Reject = 4,
    Cancel = 5,
    UploadRawContract = 6,
    SubmitInstallments = 7,
    SubmitSignedPackage = 8,
    UploadFinalContract = 9,
    RegisterPayment = 10,
    Archive = 11,
    StageRollback = 12
}
