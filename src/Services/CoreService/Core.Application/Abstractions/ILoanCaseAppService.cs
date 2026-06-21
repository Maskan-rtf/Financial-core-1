using BuildingBlocks.Application.Results;
using Core.Application.DTOs;
using Core.Application.Requests;
using Core.Application.Responses;

namespace Core.Application.Abstractions;

public interface ILoanCaseAppService
{
    Task<Result<LoanCaseDto>> CreateAsync(CreateLoanCaseRequest request, CancellationToken ct);
    Task<Result<LoanCaseDto>> GetAsync(Guid caseId, CancellationToken ct);
    Task<Result<LoanCaseDto>> UpdateTitleAsync(Guid caseId, UpdateCaseTitleRequest request, CancellationToken ct);
    Task<Result<PagedResult<LoanCaseDto>>> GetPagedAsync(GetLoanCasesRequest request, CancellationToken ct);
    Task<Result<IEnumerable<LoanWorkflowHistoryDto>>> GetHistoryAsync(Guid caseId, CancellationToken ct);
    Task<Result> UpdateApplicationAsync(Guid caseId, UpdateLoanApplicationRequest request, CancellationToken ct);
    Task<Result> BeginDataEntryAsync(Guid caseId, CancellationToken ct);
    Task<Result> SubmitApplicationAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> UpdateApprovalDetailAsync(Guid caseId, UpdateLoanApprovalDetailRequest request, CancellationToken ct);
    Task<Result> ApproveCreditReviewAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct);
    Task<Result> RequestCreditRevisionAsync(Guid caseId, string message, CancellationToken ct);
    Task<Result> CeoApproveInitialAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> CeoRejectInitialAsync(Guid caseId, string reason, CancellationToken ct);
    Task<Result> UpsertInstallmentsAsync(Guid caseId, UpsertLoanInstallmentsRequest request, CancellationToken ct);
    Task<Result> CompleteLegalSetupAsync(Guid caseId, CancellationToken ct);
    Task<Result> SubmitSignedPackageAsync(Guid caseId, CancellationToken ct);
    Task<Result> ApproveLegalReviewAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct);
    Task<Result> RequestLegalRevisionAsync(Guid caseId, string message, CancellationToken ct);
    Task<Result> ApproveFinancialReviewAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct);
    Task<Result> RequestFinancialRevisionAsync(Guid caseId, string message, CancellationToken ct);
    Task<Result> ConfirmFinalContractUploadedAsync(Guid caseId, CancellationToken ct);
    Task<Result> CeoApproveFinalAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> CeoRejectFinalAsync(Guid caseId, string reason, CancellationToken ct);
    Task<Result> RegisterPaymentAsync(Guid caseId, RegisterLoanPaymentRequest request, CancellationToken ct);
    Task<Result<IEnumerable<LoanPaymentDto>>> ListPaymentsAsync(Guid caseId, CancellationToken ct);
    Task<Result<IEnumerable<LoanInstallmentDto>>> ListInstallmentsAsync(Guid caseId, CancellationToken ct);
    Task<Result> MarkInstallmentPaidAsync(Guid caseId, Guid installmentId, MarkLoanInstallmentPaidRequest request, CancellationToken ct);
    Task<Result> CompleteRepaymentAsync(Guid caseId, CancellationToken ct);
    Task<Result<PresignLoanUploadResponse>> PresignDocumentUploadAsync(Guid caseId, PresignLoanUploadRequest request, CancellationToken ct);
    Task<Result<LoanCaseDocumentDto>> ConfirmDocumentUploadedAsync(Guid caseId, string s3Key, string? originalFileName, CancellationToken ct);
    Task<Result<IEnumerable<LoanCaseDocumentDto>>> ListDocumentsAsync(Guid caseId, CancellationToken ct);
    Task<Result<DocumentDownloadFileResult>> DownloadDocumentFileAsync(Guid caseId, Guid documentId, CancellationToken ct);
    Task<Result<IEnumerable<LoanCaseCommentDto>>> ListCommentsAsync(Guid caseId, bool includeInternal, CancellationToken ct);
}
