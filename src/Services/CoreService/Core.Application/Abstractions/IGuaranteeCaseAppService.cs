using BuildingBlocks.Application.Results;
using Core.Application.DTOs;
using Core.Application.Requests;
using Core.Application.Responses;

namespace Core.Application.Abstractions;

public interface IGuaranteeCaseAppService
{
    Task<Result<GuaranteeCaseDto>> CreateAsync(CreateGuaranteeCaseRequest request, CancellationToken ct);
    Task<Result<GuaranteeCaseDto>> GetAsync(Guid caseId, CancellationToken ct);
    Task<Result<GuaranteeCaseDto>> UpdateTitleAsync(Guid caseId, UpdateCaseTitleRequest request, CancellationToken ct);
    Task<Result<PagedResult<GuaranteeCaseDto>>> GetPagedAsync(GetGuaranteeCasesRequest request, CancellationToken ct);
    Task<Result<IEnumerable<GuaranteeWorkflowHistoryDto>>> GetHistoryAsync(Guid caseId, CancellationToken ct);
    Task<Result> UpdateApplicationAsync(Guid caseId, UpdateGuaranteeApplicationRequest request, CancellationToken ct);
    Task<Result> BeginDataEntryAsync(Guid caseId, CancellationToken ct);
    Task<Result> SubmitApplicationAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> ApproveCreditReviewAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct);
    Task<Result> RequestCreditRevisionAsync(Guid caseId, string message, CancellationToken ct);
    Task<Result<GuaranteeFundCreditLimitDto>> GetFundCreditLimitAsync(CancellationToken ct);

    Task<Result<GuaranteeFundCreditLimitDto>> SetFundCreditLimitAsync(
        SetGuaranteeFundCreditLimitRequest request,
        CancellationToken ct);

    Task<Result<GuaranteeFundCreditLimitDto>> SetApplicantCreditLimitAsync(
        Guid caseId,
        SetGuaranteeApplicantCreditLimitRequest request,
        CancellationToken ct);

    Task<Result<GuaranteeFundCreditLimitDto>> GetApplicantCreditLimitAsync(Guid caseId, CancellationToken ct);

    Task<Result> UpdateApprovalFormAsync(Guid caseId, UpdateGuaranteeApprovalFormRequest request, CancellationToken ct);
    Task<Result> SubmitApprovalFormAsync(Guid caseId, CancellationToken ct);
    Task<Result> CeoApproveInitialAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> CeoRejectInitialAsync(Guid caseId, string reason, CancellationToken ct);
    Task<Result> CeoCancelInitialAsync(Guid caseId, string reason, CancellationToken ct);
    Task<Result> CancelAsync(Guid caseId, string reason, CancellationToken ct);
    Task<Result> ConfirmDraftContractUploadedAsync(Guid caseId, CancellationToken ct);
    Task<Result> SubmitSignedPackageAsync(Guid caseId, CancellationToken ct);
    Task<Result> ApproveAttachmentsAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct);
    Task<Result> RequestAttachmentRevisionAsync(Guid caseId, string message, CancellationToken ct);
    Task<Result> ConfirmFinalContractUploadedAsync(Guid caseId, CancellationToken ct);
    Task<Result> CeoApproveFinalAsync(Guid caseId, string? comment, CancellationToken ct);
    Task<Result> CeoRejectOrCancelFinalAsync(Guid caseId, string reason, bool cancel, CancellationToken ct);
    Task<Result> ConfirmIssuanceDocumentsUploadedAsync(Guid caseId, CancellationToken ct);
    Task<Result<PresignGuaranteeUploadResponse>> PresignDocumentUploadAsync(Guid caseId, PresignGuaranteeUploadRequest request, CancellationToken ct);
    Task<Result<GuaranteeCaseDocumentDto>> ConfirmDocumentUploadedAsync(Guid caseId, string s3Key, string? originalFileName, CancellationToken ct);
    Task<Result<IEnumerable<GuaranteeCaseDocumentDto>>> ListDocumentsAsync(Guid caseId, CancellationToken ct);
    Task<Result<DocumentDownloadFileResult>> DownloadDocumentFileAsync(Guid caseId, Guid documentId, CancellationToken ct);
    Task<Result<IEnumerable<GuaranteeCaseCommentDto>>> ListCommentsAsync(Guid caseId, bool includeInternal, CancellationToken ct);
}
