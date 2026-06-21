using System.Net;
using Asp.Versioning;
using BuildingBlocks.Application.Results;
using Core.API.Http;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.DTOs;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.API.Controllers;

/// <summary>
/// Investment cases — workflow, documents, kanban, and payments (fund platform module).
/// </summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/investmentcases")]
public sealed class InvestmentCasesController(
    IInvestmentCaseAppService service,
    IKanbanAppService kanbanService) : ApiControllerBase
{
    #region Kanban

    [HttpGet("kanban/action-required")]
    [Authorize]
    public async Task<IActionResult> GetKanbanActionRequired(CancellationToken ct)
    {
        var result = await kanbanService.GetActionRequiredInvestmentOnlyAsync(ct);
        return Respond(result, CaseSuccessMessages.KanbanActionRequiredRetrieved);
    }

    [HttpGet("kanban/watching")]
    [Authorize]
    public async Task<IActionResult> GetKanbanWatching(CancellationToken ct)
    {
        var result = await kanbanService.GetWatchingInvestmentOnlyAsync(ct);
        return Respond(result, CaseSuccessMessages.KanbanWatchingRetrieved);
    }

    #endregion

    #region Core Management

    [HttpPost]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> Create([FromBody] CreateInvestmentCaseRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return Respond(result, CaseSuccessMessages.InvestmentCaseCreated, HttpStatusCode.Created);
    }

    [HttpPut("{id:guid}/title")]
    [Authorize]
    public async Task<IActionResult> UpdateTitle(Guid id, [FromBody] UpdateCaseTitleRequest request, CancellationToken ct)
    {
        var result = await service.UpdateTitleAsync(id, request, ct);
        return Respond(result, CaseSuccessMessages.CaseTitleUpdated);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        return Respond(result, CaseSuccessMessages.InvestmentCaseRetrieved);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPaged([FromQuery] GetInvestmentCasesRequest request, CancellationToken ct)
    {
        var result = await service.GetPagedAsync(request, ct);
        return Respond(result, CaseSuccessMessages.InvestmentCasesRetrieved);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        var result = await service.GetHistoryAsync(id, ct);
        return Respond(result, CaseSuccessMessages.CaseHistoryRetrieved);
    }

    #endregion

    #region Data Entry 1 & 2

    [HttpPut("{id:guid}/data-entry1")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> UpdateDataEntry1(Guid id, [FromBody] UpdateDataEntry1Request request, CancellationToken ct)
    {
        var result = await service.UpdateDataEntry1Async(id, request, ct);
        return Respond(result, CaseSuccessMessages.DataEntry1Updated);
    }

    [HttpPost("{id:guid}/data-entry1/submit")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> SubmitDataEntry1(Guid id, CancellationToken ct)
    {
        var request = await ReadTransitionRequestAsync(ct);
        var result = await service.SubmitDataEntry1Async(id, request.Comment, ct);
        return Respond(result, CaseSuccessMessages.DataEntry1Submitted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/data-entry1/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveDataEntry1(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveDataEntry1Async(id, request.Comment, request.InternalComment, ct);
        return Respond(result, CaseSuccessMessages.DataEntry1Approved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/data-entry1/revision-request")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestDataEntry1Revision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestDataEntry1RevisionAsync(id, request.Message, ct);
        return Respond(result, CaseSuccessMessages.DataEntry1RevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpPut("{id:guid}/data-entry2")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> UpdateDataEntry2(Guid id, [FromBody] UpdateDataEntry2Request request, CancellationToken ct)
    {
        var result = await service.UpdateDataEntry2Async(id, request, ct);
        return Respond(result, CaseSuccessMessages.DataEntry2Updated);
    }

    [HttpPost("{id:guid}/data-entry2/submit")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> SubmitDataEntry2(Guid id, CancellationToken ct)
    {
        var request = await ReadTransitionRequestAsync(ct);
        var result = await service.SubmitDataEntry2Async(id, request.Comment, ct);
        return Respond(result, CaseSuccessMessages.DataEntry2Submitted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/data-entry2/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveDataEntry2(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveDataEntry2Async(id, request.Comment, request.InternalComment, ct);
        return Respond(result, CaseSuccessMessages.DataEntry2Approved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/data-entry2/revision-request")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestDataEntry2Revision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestDataEntry2RevisionAsync(id, request.Message, ct);
        return Respond(result, CaseSuccessMessages.DataEntry2RevisionRequested, HttpStatusCode.Accepted);
    }

    #endregion

    #region Valuations

    [HttpPost("{id:guid}/valuations")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RecordValuation(Guid id, [FromBody] RecordValuationRequest request, CancellationToken ct)
    {
        var result = await service.RecordValuationAsync(id, request, ct);
        return Respond(result, CaseSuccessMessages.ValuationRecorded);
    }

    [HttpPost("{id:guid}/valuations/initial/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveInitialValuation(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveInitialValuationAsync(id, request.Comment, ct);
        return Respond(result, CaseSuccessMessages.InitialValuationApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/valuations/secondary/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveSecondaryValuation(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveSecondaryValuationAsync(id, request.Comment, ct);
        return Respond(result, CaseSuccessMessages.SecondaryValuationApproved, HttpStatusCode.Accepted);
    }

    #endregion

    #region Legal & Contracts

    /// <summary>Legacy alias for documents/confirm when the file is already in storage. Prefer presign + PUT + confirm.</summary>
    [HttpPost("{id:guid}/contracts/preliminary/upload")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> UploadPreliminaryContract(Guid id, [FromQuery] string s3Key, CancellationToken ct)
    {
        var result = await service.UploadPreliminaryContractAsync(id, s3Key, ct);
        return Respond(result, CaseSuccessMessages.PreliminaryContractUploaded);
    }

    [HttpPost("{id:guid}/contracts/preliminary/approve")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> ApprovePreliminaryContract(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApprovePreliminaryContractAsync(id, request.Comment, ct);
        return Respond(result, CaseSuccessMessages.PreliminaryContractApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/contracts/preliminary/revision-request")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> RequestPreliminaryContractRevision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestPreliminaryContractRevisionAsync(id, request.Message, ct);
        return Respond(result, CaseSuccessMessages.PreliminaryContractRevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/contracts/finalize-draft")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> FinalizeContractDraft(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.FinalizeContractDraftAsync(id, request.Comment, ct);
        return Respond(result, CaseSuccessMessages.ContractDraftFinalized, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/contracts/confirm-signature")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ConfirmSignature(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ConfirmSignatureAsync(id, request.Comment, ct);
        return Respond(result, CaseSuccessMessages.ContractSignatureConfirmed, HttpStatusCode.Accepted);
    }

    /// <summary>Legacy alias for documents/confirm when the file is already in storage. Prefer presign + PUT + confirm.</summary>
    [HttpPost("{id:guid}/contracts/signed/upload")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> UploadSignedContract(Guid id, [FromQuery] string s3Key, CancellationToken ct)
    {
        var result = await service.UploadSignedContractAsync(id, s3Key, ct);
        return Respond(result, CaseSuccessMessages.SignedContractUploaded);
    }

    #endregion

    #region Finance & Payments

    [HttpPut("{id:guid}/financial-worksheet")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> UpdateFinancialWorksheet(Guid id, [FromBody] UpdateFinancialWorksheetRequest request, CancellationToken ct)
    {
        var result = await service.UpdateFinancialWorksheetAsync(id, request, ct);
        return Respond(result, CaseSuccessMessages.FinancialWorksheetUpdated);
    }

    [HttpPost("{id:guid}/financial-worksheet/submit")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> SubmitFinancialWorksheet(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.SubmitFinancialWorksheetAsync(id, request.Comment, ct);
        return Respond(result, CaseSuccessMessages.FinancialWorksheetSubmitted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/financial-worksheet/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveFinancialWorksheet(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveFinancialWorksheetAsync(id, request.Comment, request.InternalComment, ct);
        return Respond(result, CaseSuccessMessages.FinancialWorksheetApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/financial-worksheet/revision-request")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestFinancialWorksheetRevision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestFinancialWorksheetRevisionAsync(id, request.Message, ct);
        return Respond(result, CaseSuccessMessages.FinancialWorksheetRevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo-approval/approve")]
    [Authorize(Policy = "InvestmentCases.CeoApprove")]
    public async Task<IActionResult> ApproveCeo(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveCeoAsync(id, request.Comment, ct);
        return Respond(result, CaseSuccessMessages.CeoApprovalGranted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo-approval/revision-request")]
    [Authorize(Policy = "InvestmentCases.CeoApprove")]
    public async Task<IActionResult> RequestCeoRevision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestCeoRevisionAsync(id, request.Message, ct);
        return Respond(result, CaseSuccessMessages.CeoRevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpGet("{id:guid}/payments")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> GetPayments(Guid id, CancellationToken ct)
    {
        var result = await service.GetPaymentsAsync(id, ct);
        return Respond(result, CaseSuccessMessages.PaymentsRetrieved);
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        var result = await service.RecordPaymentAsync(id, request, ct);
        return Respond(result, CaseSuccessMessages.PaymentRecorded);
    }

    [HttpPost("{id:guid}/payments/{paymentId:guid}/confirm")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ConfirmPayment(Guid id, Guid paymentId, CancellationToken ct)
    {
        var result = await service.ConfirmPaymentAsync(id, paymentId, ct);
        return Respond(result, CaseSuccessMessages.PaymentConfirmed);
    }

    [HttpPost("{id:guid}/payments/{paymentId:guid}/cancel")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> CancelPayment(Guid id, Guid paymentId, CancellationToken ct)
    {
        var result = await service.CancelPaymentAsync(id, paymentId, ct);
        return Respond(result, CaseSuccessMessages.PaymentCancelled);
    }

    #endregion

    #region Negative Actions

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] SemanticRejectRequest request, CancellationToken ct)
    {
        var result = await service.RejectAsync(id, request.Reason, ct);
        return Respond(result, CaseSuccessMessages.InvestmentCaseRejected, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] SemanticCancelRequest request, CancellationToken ct)
    {
        var result = await service.CancelAsync(id, request.Reason, ct);
        return Respond(result, CaseSuccessMessages.InvestmentCaseCancelled, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize]
    public async Task<IActionResult> Archive(Guid id, [FromBody] SemanticArchiveRequest request, CancellationToken ct)
    {
        var result = await service.ArchiveAsync(id, request.Reason, ct);
        return Respond(result, CaseSuccessMessages.InvestmentCaseArchived, HttpStatusCode.Accepted);
    }

    #endregion

    #region Documents & Evaluations

    [HttpPost("{id:guid}/documents/presign")]
    [Authorize]
    public async Task<IActionResult> Presign(Guid id, [FromBody] PresignUploadRequest request, CancellationToken ct)
    {
        var result = await service.PresignDocumentUploadAsync(id, request, ct);
        return Respond(result, CaseSuccessMessages.DocumentUploadPresigned);
    }

    [HttpPost("{id:guid}/documents/upload")]
    [Authorize]
    [RequestSizeLimit(250L * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(Guid id, [FromForm] DocumentType documentType, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { success = false, message = ApiMessages.InvalidFileName });

        await using var stream = file.OpenReadStream();
        var request = new PresignUploadRequest(documentType, file.FileName, file.ContentType, file.Length);
        var result = await service.UploadDocumentAsync(id, request, stream, ct);
        return Respond(result, CaseSuccessMessages.DocumentUploaded);
    }

    /// <summary>Registers the object in DB after client PUT to presigned URL; advances workflow for contract document types when status allows.</summary>
    [HttpPost("{id:guid}/documents/confirm")]
    [Authorize]
    public async Task<IActionResult> Confirm(Guid id, [FromQuery] string s3Key, [FromQuery] string? originalFileName, CancellationToken ct)
    {
        var result = await service.ConfirmDocumentUploadedAsync(id, s3Key, originalFileName, ct);
        return Respond(result, CaseSuccessMessages.DocumentUploadConfirmed);
    }

    [HttpGet("{id:guid}/documents")]
    [Authorize]
    public async Task<IActionResult> GetDocuments(Guid id, CancellationToken ct)
    {
        var result = await service.GetDocumentsAsync(id, ct);
        return Respond(result, CaseSuccessMessages.DocumentsRetrieved);
    }

    [HttpGet("{id:guid}/documents/latest")]
    [Authorize]
    public async Task<IActionResult> GetDocumentsLatest(Guid id, CancellationToken ct)
    {
        var result = await service.GetDocumentsLatestAsync(id, ct);
        return Respond(result, CaseSuccessMessages.DocumentsLatestRetrieved);
    }

    [HttpGet("{id:guid}/documents/version-groups")]
    [Authorize]
    public async Task<IActionResult> GetDocumentVersionGroups(Guid id, [FromQuery] string? scope, CancellationToken ct)
    {
        var result = await service.GetDocumentVersionGroupsAsync(id, scope, ct);
        return Respond(result, CaseSuccessMessages.DocumentVersionGroupsRetrieved);
    }

    [HttpGet("{id:guid}/documents/types/{documentType:int}/versions")]
    [Authorize]
    public async Task<IActionResult> GetDocumentVersions(Guid id, int documentType, CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(DocumentType), documentType))
            return BadRequest(new { success = false, message = ApiMessages.InvalidDocumentType });

        var result = await service.GetDocumentVersionsAsync(id, (DocumentType)documentType, ct);
        return Respond(result, CaseSuccessMessages.DocumentVersionsRetrieved);
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    [Authorize]
    public async Task<IActionResult> DownloadDocument(
        Guid id,
        Guid documentId,
        [FromQuery] bool presign = false,
        CancellationToken ct = default)
    {
        if (presign)
        {
            var presignResult = await service.PresignDocumentDownloadAsync(id, documentId, ct);
            return Respond(presignResult, CaseSuccessMessages.DocumentDownloadPresigned);
        }

        var result = await service.DownloadDocumentFileAsync(id, documentId, ct);
        if (result.IsFailure)
            return Respond(result, CaseSuccessMessages.DocumentDownloadPresigned);

        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("{id:guid}/evaluations")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> UpsertEvaluation(Guid id, [FromBody] CaseEvaluationUpsertRequest request, CancellationToken ct)
    {
        var result = await service.UpsertEvaluationAsync(id, request, ct);
        return Respond(result, CaseSuccessMessages.EvaluationSaved);
    }

    [HttpGet("{id:guid}/evaluations")]
    [Authorize]
    public async Task<IActionResult> GetEvaluations(Guid id, CancellationToken ct)
    {
        var result = await service.GetEvaluationsAsync(id, ct);
        return Respond(result, CaseSuccessMessages.EvaluationsRetrieved);
    }

    #endregion

    #region Comments

    [HttpGet("{id:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> GetComments(Guid id, [FromQuery] bool includeInternal = false, CancellationToken ct = default)
    {
        var result = await service.GetCommentsAsync(id, includeInternal, ct);
        return Respond(result, CaseSuccessMessages.CommentsRetrieved);
    }

    [HttpPost("{id:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        var result = await service.AddCommentAsync(id, request, ct);
        return Respond(result, CaseSuccessMessages.CommentAdded);
    }

    [HttpPost("{id:guid}/comments/{commentId:guid}/attachments")]
    [Authorize]
    public async Task<IActionResult> AddCommentAttachment(Guid id, Guid commentId, [FromQuery] string s3Key, [FromQuery] string fileName, CancellationToken ct)
    {
        var result = await service.AddCommentAttachmentAsync(id, commentId, s3Key, fileName, ct);
        return Respond(result, CaseSuccessMessages.CommentAttachmentAdded);
    }

    #endregion
}
