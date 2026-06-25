using BuildingBlocks.Application.Results;
using Core.Application.DTOs;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Domain.Enums;


namespace Core.Application.Abstractions;

public interface IInvestmentCaseAppService
{
    Task<Result<InvestmentCaseDto>> CreateAsync(CreateInvestmentCaseRequest request, CancellationToken cancellationToken);
    Task<Result<InvestmentCaseDto>> GetAsync(Guid caseId, CancellationToken cancellationToken);
    Task<Result<InvestmentCaseDto>> UpdateTitleAsync(Guid caseId, UpdateCaseTitleRequest request, CancellationToken cancellationToken);

    Task<Result> UpdateDataEntry1Async(Guid caseId, UpdateDataEntry1Request request, CancellationToken cancellationToken);
    Task<Result> UpdateDataEntry2Async(Guid caseId, UpdateDataEntry2Request request, CancellationToken cancellationToken);
    Task<Result> UpdateFinancialWorksheetAsync(Guid caseId, UpdateFinancialWorksheetRequest request, CancellationToken cancellationToken);
    Task<Result<CaseFinancialWorksheetDto>> GetFinancialWorksheetAsync(Guid caseId, CancellationToken cancellationToken);

    Task<Result> SubmitDataEntry1Async(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> ApproveDataEntry1Async(Guid caseId, string? comment, string? internalComment, CancellationToken ct);
    Task<Result> RequestDataEntry1RevisionAsync(Guid caseId, string message, CancellationToken ct);

    Task<Result> SubmitDataEntry2Async(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> ApproveDataEntry2Async(Guid caseId, string? comment, string? internalComment, CancellationToken ct);
    Task<Result> RequestDataEntry2RevisionAsync(Guid caseId, string message, CancellationToken ct);

    Task<Result> ApproveInitialValuationAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> ApproveSecondaryValuationAsync(Guid caseId, string? comment, CancellationToken ct);

    Task<Result> UploadPreliminaryContractAsync(Guid caseId, string s3Key, CancellationToken ct);
    Task<Result> ApprovePreliminaryContractAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> RequestPreliminaryContractRevisionAsync(Guid caseId, string message, CancellationToken ct);

    Task<Result> FinalizeContractDraftAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> ConfirmSignatureAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> UploadSignedContractAsync(Guid caseId, string s3Key, CancellationToken ct);

    Task<Result> SubmitFinancialWorksheetAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> ApproveFinancialWorksheetAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct);
    Task<Result> RequestFinancialWorksheetRevisionAsync(Guid caseId, string message, CancellationToken ct);
    Task<Result> ApproveCeoAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> RequestCeoRevisionAsync(Guid caseId, string message, CancellationToken ct);

    Task<Result> ConfirmPaymentAsync(Guid caseId, Guid paymentId, CancellationToken ct);
    Task<Result> CancelPaymentAsync(Guid caseId, Guid paymentId, CancellationToken ct);

    Task<Result> RejectAsync(Guid caseId, string reason, CancellationToken ct);
    Task<Result> CancelAsync(Guid caseId, string reason, CancellationToken ct);
    Task<Result> ArchiveAsync(Guid caseId, string reason, CancellationToken ct);

    Task<Result<IEnumerable<CaseCommentDto>>> GetCommentsAsync(Guid caseId, bool includeInternal, CancellationToken ct);
    Task<Result> AddCommentAsync(Guid caseId, AddCommentRequest request, CancellationToken cancellationToken);
    Task<Result> AddCommentAttachmentAsync(Guid caseId, Guid commentId, string s3Key, string fileName, CancellationToken cancellationToken);

    Task<Result<IEnumerable<CaseDocumentDto>>> GetDocumentsAsync(Guid caseId, CancellationToken cancellationToken);
    Task<Result<IEnumerable<CaseDocumentDto>>> GetDocumentsLatestAsync(Guid caseId, CancellationToken cancellationToken);
    Task<Result<CaseDocumentTypeVersionsDto>> GetDocumentVersionsAsync(Guid caseId, DocumentType documentType, CancellationToken cancellationToken);
    Task<Result<IEnumerable<CaseDocumentTypeVersionsDto>>> GetDocumentVersionGroupsAsync(Guid caseId, string? scope, CancellationToken cancellationToken);
    Task<Result<IEnumerable<CaseWorkflowHistoryDto>>> GetHistoryAsync(Guid caseId, CancellationToken cancellationToken);

    Task<Result> UpsertEvaluationAsync(Guid caseId, CaseEvaluationUpsertRequest request, CancellationToken cancellationToken);
    Task<Result<IEnumerable<CaseEvaluationDto>>> GetEvaluationsAsync(Guid caseId, CancellationToken cancellationToken);

    Task<Result<PagedResult<InvestmentCaseDto>>> GetPagedAsync(GetInvestmentCasesRequest request, CancellationToken cancellationToken);

    Task<Result<PresignUploadResponse>> PresignDocumentUploadAsync(Guid caseId, PresignUploadRequest request, CancellationToken cancellationToken);
    Task<Result<CaseDocumentDto>> UploadDocumentAsync(Guid caseId, PresignUploadRequest request, Stream content, CancellationToken cancellationToken);
    Task<Result<CaseDocumentDto>> ConfirmDocumentUploadedAsync(Guid caseId, string s3Key, string? originalFileName, CancellationToken cancellationToken);
    Task<Result<PresignDownloadResponse>> PresignDocumentDownloadAsync(Guid caseId, Guid documentId, CancellationToken cancellationToken);
    Task<Result<DocumentDownloadFileResult>> DownloadDocumentFileAsync(Guid caseId, Guid documentId, CancellationToken cancellationToken);

    Task<Result> RecordValuationAsync(Guid caseId, RecordValuationRequest request, CancellationToken cancellationToken);
    Task<Result<CasePaymentsDto>> GetPaymentsAsync(Guid caseId, CancellationToken cancellationToken);
    Task<Result> RecordPaymentAsync(Guid caseId, RecordPaymentRequest request, CancellationToken cancellationToken);
    Task<Result> UpdatePaymentAsync(Guid caseId, Guid paymentId, UpdatePaymentRequest request, CancellationToken cancellationToken);
    Task<Result> DeletePaymentAsync(Guid caseId, Guid paymentId, CancellationToken cancellationToken);
}
