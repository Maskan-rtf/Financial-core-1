using System.Net;
using Asp.Versioning;
using BuildingBlocks.Application.Results;
using Core.API.Http;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.Requests;
using Core.Application.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.API.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/guaranteecases")]
public sealed class GuaranteeCasesController(IGuaranteeCaseAppService service) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> Create([FromBody] CreateGuaranteeCaseRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return Respond(result, GuaranteeSuccessMessages.GuaranteeCaseCreated, HttpStatusCode.Created);
    }

    [HttpPut("{id:guid}/title")]
    [Authorize]
    public async Task<IActionResult> UpdateTitle(Guid id, [FromBody] UpdateCaseTitleRequest request, CancellationToken ct)
    {
        var result = await service.UpdateTitleAsync(id, request, ct);
        return Respond(result, GuaranteeSuccessMessages.CaseTitleUpdated);
    }

    [HttpGet("fund-credit-limit")]
    [Authorize]
    public async Task<IActionResult> GetFundCreditLimit(CancellationToken ct)
    {
        var result = await service.GetFundCreditLimitAsync(ct);
        return Respond(result, GuaranteeSuccessMessages.FundCreditLimitRetrieved);
    }

    [HttpPut("fund-credit-limit")]
    [Authorize(Policy = "GuaranteeCases.CeoOnly")]
    public async Task<IActionResult> SetFundCreditLimit(
        [FromBody] SetGuaranteeFundCreditLimitRequest request,
        CancellationToken ct)
    {
        var result = await service.SetFundCreditLimitAsync(request, ct);
        return Respond(result, GuaranteeSuccessMessages.FundCreditLimitSet);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.GuaranteeCaseRetrieved);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPaged([FromQuery] GetGuaranteeCasesRequest request, CancellationToken ct)
    {
        var result = await service.GetPagedAsync(request, ct);
        return Respond(result, GuaranteeSuccessMessages.GuaranteeCasesRetrieved);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        var result = await service.GetHistoryAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.CaseHistoryRetrieved);
    }

    [HttpGet("{id:guid}/amendment/details")]
    [Authorize]
    public async Task<IActionResult> GetAmendment(Guid id, CancellationToken ct)
    {
        var result = await service.GetAmendmentAsync(id, ct);
        return Respond(result, GuaranteeAmendmentMessages.SuccessRetrieved);
    }

    [HttpGet("{id:guid}/amendment/cancellation/details")]
    [Authorize]
    public async Task<IActionResult> GetCancellationDetails(Guid id, CancellationToken ct)
    {
        var result = await service.GetCancellationDetailsAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.CancellationDetailsRetrieved);
    }

    [HttpPut("{id:guid}/application")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> UpdateApplication(Guid id, [FromBody] UpdateGuaranteeApplicationRequest request, CancellationToken ct)
    {
        var result = await service.UpdateApplicationAsync(id, request, ct);
        return Respond(result, GuaranteeSuccessMessages.ApplicationUpdated);
    }

    [HttpPost("{id:guid}/amendment/create")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> CreateAmendment(Guid id, [FromBody] CreateGuaranteeAmendmentRequest request, CancellationToken ct)
    {
        var result = await service.CreateOrUpdateAmendmentAsync(id, request, ct);
        return Respond(result, GuaranteeAmendmentMessages.SuccessCreated);
    }

    [HttpPost("{id:guid}/amendment/cancellation/create")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> CreateCancellation(Guid id, [FromBody] CreateGuaranteeCancellationRequest request, CancellationToken ct)
    {
        var result = await service.CreateCancellationAsync(id, request, ct);
        return Respond(result, GuaranteeSuccessMessages.CancellationCreated);
    }

    [HttpPost("{id:guid}/amendment/submit")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> SubmitAmendment(Guid id, CancellationToken ct)
    {
        var result = await service.SubmitAmendmentAsync(id, ct);
        return Respond(result, GuaranteeAmendmentMessages.SuccessSubmitted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/amendment/cancellation/submit")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> SubmitCancellation(Guid id, [FromBody] SubmitGuaranteeCancellationRequest request, CancellationToken ct)
    {
        var result = await service.SubmitCancellationAsync(id, request, ct);
        return Respond(result, GuaranteeSuccessMessages.CancellationSubmitted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/amendment/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveAmendment(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveAmendmentAsync(id, request.Comment, request.InternalComment, ct);
        return Respond(result, GuaranteeAmendmentMessages.SuccessApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/amendment/cancellation/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveCancellation(Guid id, [FromBody] ApproveGuaranteeCancellationRequest request, CancellationToken ct)
    {
        var result = await service.ApproveCancellationAsync(id, request, ct);
        return Respond(result, GuaranteeSuccessMessages.CancellationApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/amendment/reject")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RejectAmendment(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RejectAmendmentAsync(id, request.Message, ct);
        return Respond(result, GuaranteeAmendmentMessages.SuccessRejected, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/amendment/revision-request")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestAmendmentRevision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestAmendmentRevisionAsync(id, request.Message, ct);
        return Respond(result, GuaranteeAmendmentMessages.SuccessRevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/amendment/approve")]
    [Authorize(Policy = "GuaranteeCases.CeoApprove")]
    public async Task<IActionResult> CeoApproveAmendment(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.CeoApproveAmendmentAsync(id, request.Comment, ct);
        return Respond(result, GuaranteeSuccessMessages.CeoAmendmentApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/amendment/reject")]
    [Authorize(Policy = "GuaranteeCases.CeoApprove")]
    public async Task<IActionResult> CeoRejectAmendment(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.CeoRejectAmendmentAsync(id, request.Message, ct);
        return Respond(result, GuaranteeAmendmentMessages.SuccessRejected, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/application/begin")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> BeginDataEntry(Guid id, CancellationToken ct)
    {
        var result = await service.BeginDataEntryAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.ApplicationUpdated, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/application/submit")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> SubmitApplication(Guid id, CancellationToken ct)
    {
        var request = await ReadTransitionRequestAsync(ct);
        var result = await service.SubmitApplicationAsync(id, request.Comment, ct);
        return Respond(result, GuaranteeSuccessMessages.ApplicationSubmitted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/credit/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveCredit(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveCreditReviewAsync(id, request.Comment, request.InternalComment, ct);
        return Respond(result, GuaranteeSuccessMessages.CreditReviewApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/credit/revision-request")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestCreditRevision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestCreditRevisionAsync(id, request.Message, ct);
        return Respond(result, GuaranteeSuccessMessages.CreditRevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpPut("{id:guid}/approval-form")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> UpdateApprovalForm(Guid id, [FromBody] UpdateGuaranteeApprovalFormRequest request, CancellationToken ct)
    {
        var result = await service.UpdateApprovalFormAsync(id, request, ct);
        return Respond(result, GuaranteeSuccessMessages.ApprovalFormUpdated);
    }

    [HttpPut("{id:guid}/applicant-credit-limit")]
    [Authorize(Policy = "GuaranteeCases.CeoOnly")]
    public async Task<IActionResult> SetApplicantCreditLimit(
        Guid id,
        [FromBody] SetGuaranteeApplicantCreditLimitRequest request,
        CancellationToken ct)
    {
        var result = await service.SetApplicantCreditLimitAsync(id, request, ct);
        return Respond(result, GuaranteeSuccessMessages.ApplicantCreditLimitSet);
    }

    [HttpGet("{id:guid}/applicant-credit-limit")]
    [Authorize]
    public async Task<IActionResult> GetApplicantCreditLimit(Guid id, CancellationToken ct)
    {
        var result = await service.GetApplicantCreditLimitAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.ApplicantCreditLimitRetrieved);
    }

    [HttpPost("{id:guid}/approval-form/submit")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> SubmitApprovalForm(Guid id, CancellationToken ct)
    {
        var result = await service.SubmitApprovalFormAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.ApprovalFormSubmitted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/initial/approve")]
    [Authorize(Policy = "GuaranteeCases.CeoApprove")]
    public async Task<IActionResult> CeoApproveInitial(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.CeoApproveInitialAsync(id, request.Comment, ct);
        return Respond(result, GuaranteeSuccessMessages.CeoInitialApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/initial/reject")]
    [Authorize(Policy = "GuaranteeCases.CeoApprove")]
    public async Task<IActionResult> CeoRejectInitial(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.CeoRejectInitialAsync(id, request.Message, ct);
        return Respond(result, GuaranteeSuccessMessages.CaseRejected, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/initial/cancel")]
    [Authorize(Policy = "GuaranteeCases.CeoApprove")]
    public async Task<IActionResult> CeoCancelInitial(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.CeoCancelInitialAsync(id, request.Message, ct);
        return Respond(result, GuaranteeSuccessMessages.CaseCancelled, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] SemanticCancelRequest request, CancellationToken ct)
    {
        var result = await service.CancelAsync(id, request.Reason, ct);
        return Respond(result, GuaranteeSuccessMessages.CaseCancelled, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/legal/draft-uploaded")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ConfirmDraftUploaded(Guid id, CancellationToken ct)
    {
        var result = await service.ConfirmDraftContractUploadedAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.DocumentConfirmed, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/signed-package/submit")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> SubmitSignedPackage(Guid id, CancellationToken ct)
    {
        var result = await service.SubmitSignedPackageAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.SignedPackageSubmitted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/attachments/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveAttachments(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveAttachmentsAsync(id, request.Comment, request.InternalComment, ct);
        return Respond(result, GuaranteeSuccessMessages.AttachmentsApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/attachments/revision-request")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestAttachmentRevision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestAttachmentRevisionAsync(id, request.Message, ct);
        return Respond(result, GuaranteeSuccessMessages.AttachmentRevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/legal/final-uploaded")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ConfirmFinalUploaded(Guid id, CancellationToken ct)
    {
        var result = await service.ConfirmFinalContractUploadedAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.DocumentConfirmed, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/final/approve")]
    [Authorize(Policy = "GuaranteeCases.CeoApprove")]
    public async Task<IActionResult> CeoApproveFinal(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.CeoApproveFinalAsync(id, request.Comment, ct);
        return Respond(result, GuaranteeSuccessMessages.CeoFinalApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/final/reject")]
    [Authorize(Policy = "GuaranteeCases.CeoApprove")]
    public async Task<IActionResult> CeoRejectFinal(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.CeoRejectOrCancelFinalAsync(id, request.Message, cancel: false, ct);
        return Respond(result, GuaranteeSuccessMessages.CaseRejected, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/final/cancel")]
    [Authorize(Policy = "GuaranteeCases.CeoApprove")]
    public async Task<IActionResult> CeoCancelFinal(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.CeoRejectOrCancelFinalAsync(id, request.Message, cancel: true, ct);
        return Respond(result, GuaranteeSuccessMessages.CaseCancelled, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/issuance/uploaded")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ConfirmIssuanceUploaded(Guid id, CancellationToken ct)
    {
        var result = await service.ConfirmIssuanceDocumentsUploadedAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.IssuanceDocumentsUploaded, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/documents/presign")]
    [Authorize]
    public async Task<IActionResult> Presign(Guid id, [FromBody] PresignGuaranteeUploadRequest request, CancellationToken ct)
    {
        var result = await service.PresignDocumentUploadAsync(id, request, ct);
        return Respond(result, GuaranteeSuccessMessages.DocumentPresigned);
    }

    [HttpPost("{id:guid}/documents/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmDocument(Guid id, [FromQuery] string s3Key, [FromQuery] string? originalFileName, CancellationToken ct)
    {
        var result = await service.ConfirmDocumentUploadedAsync(id, s3Key, originalFileName, ct);
        return Respond(result, GuaranteeSuccessMessages.DocumentConfirmed);
    }

    [HttpGet("{id:guid}/documents")]
    [Authorize]
    public async Task<IActionResult> ListDocuments(Guid id, CancellationToken ct)
    {
        var result = await service.ListDocumentsAsync(id, ct);
        return Respond(result, GuaranteeSuccessMessages.DocumentsRetrieved);
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    [Authorize]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId, CancellationToken ct)
    {
        var result = await service.DownloadDocumentFileAsync(id, documentId, ct);
        if (result.IsFailure)
            return Respond(result, GuaranteeSuccessMessages.DocumentPresigned);

        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("{id:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> ListComments(Guid id, [FromQuery] bool includeInternal, CancellationToken ct)
    {
        var result = await service.ListCommentsAsync(id, includeInternal, ct);
        return Respond(result, GuaranteeSuccessMessages.CommentsRetrieved);
    }
}
