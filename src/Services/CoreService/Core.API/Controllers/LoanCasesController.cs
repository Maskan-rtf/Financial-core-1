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
[Route("api/v{version:apiVersion}/loancases")]
public sealed class LoanCasesController(ILoanCaseAppService service) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> Create([FromBody] CreateLoanCaseRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return Respond(result, LoanSuccessMessages.LoanCaseCreated, HttpStatusCode.Created);
    }

    [HttpPut("{id:guid}/title")]
    [Authorize]
    public async Task<IActionResult> UpdateTitle(Guid id, [FromBody] UpdateCaseTitleRequest request, CancellationToken ct)
    {
        var result = await service.UpdateTitleAsync(id, request, ct);
        return Respond(result, LoanSuccessMessages.CaseTitleUpdated);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        return Respond(result, LoanSuccessMessages.LoanCaseRetrieved);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPaged([FromQuery] GetLoanCasesRequest request, CancellationToken ct)
    {
        var result = await service.GetPagedAsync(request, ct);
        return Respond(result, LoanSuccessMessages.LoanCasesRetrieved);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        var result = await service.GetHistoryAsync(id, ct);
        return Respond(result, LoanSuccessMessages.CaseHistoryRetrieved);
    }

    [HttpPut("{id:guid}/application")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> UpdateApplication(Guid id, [FromBody] UpdateLoanApplicationRequest request, CancellationToken ct)
    {
        var result = await service.UpdateApplicationAsync(id, request, ct);
        return Respond(result, LoanSuccessMessages.ApplicationUpdated);
    }

    [HttpPost("{id:guid}/application/begin")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> BeginDataEntry(Guid id, CancellationToken ct)
    {
        var result = await service.BeginDataEntryAsync(id, ct);
        return Respond(result, LoanSuccessMessages.ApplicationUpdated, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/application/submit")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> SubmitApplication(Guid id, CancellationToken ct)
    {
        var request = await ReadTransitionRequestAsync(ct);
        var result = await service.SubmitApplicationAsync(id, request.Comment, ct);
        return Respond(result, LoanSuccessMessages.ApplicationSubmitted, HttpStatusCode.Accepted);
    }

    [HttpPut("{id:guid}/approval-detail")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> UpdateApprovalDetail(Guid id, [FromBody] UpdateLoanApprovalDetailRequest request, CancellationToken ct)
    {
        var result = await service.UpdateApprovalDetailAsync(id, request, ct);
        return Respond(result, LoanSuccessMessages.ApprovalDetailUpdated);
    }

    [HttpPost("{id:guid}/credit/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveCredit(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveCreditReviewAsync(id, request.Comment, request.InternalComment, ct);
        return Respond(result, LoanSuccessMessages.CreditReviewApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/credit/revision-request")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestCreditRevision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestCreditRevisionAsync(id, request.Message, ct);
        return Respond(result, LoanSuccessMessages.CreditRevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/initial/approve")]
    [Authorize(Policy = "LoanCases.CeoApprove")]
    public async Task<IActionResult> CeoApproveInitial(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.CeoApproveInitialAsync(id, request.Comment, ct);
        return Respond(result, LoanSuccessMessages.CeoInitialApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/initial/reject")]
    [Authorize(Policy = "LoanCases.CeoApprove")]
    public async Task<IActionResult> CeoRejectInitial(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.CeoRejectInitialAsync(id, request.Message, ct);
        return Respond(result, LoanSuccessMessages.CeoInitialRejected, HttpStatusCode.Accepted);
    }

    [HttpPut("{id:guid}/installments")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> UpsertInstallments(Guid id, [FromBody] UpsertLoanInstallmentsRequest request, CancellationToken ct)
    {
        var result = await service.UpsertInstallmentsAsync(id, request, ct);
        return Respond(result, LoanSuccessMessages.InstallmentsUpdated);
    }

    [HttpGet("{id:guid}/installments")]
    [Authorize]
    public async Task<IActionResult> ListInstallments(Guid id, CancellationToken ct)
    {
        var result = await service.ListInstallmentsAsync(id, ct);
        return Respond(result, LoanSuccessMessages.InstallmentsRetrieved);
    }

    [HttpPost("{id:guid}/legal/setup-complete")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> CompleteLegalSetup(Guid id, CancellationToken ct)
    {
        var result = await service.CompleteLegalSetupAsync(id, ct);
        return Respond(result, LoanSuccessMessages.LegalSetupCompleted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/signed-package/submit")]
    [Authorize(Policy = "ApplicantOnly")]
    public async Task<IActionResult> SubmitSignedPackage(Guid id, CancellationToken ct)
    {
        var result = await service.SubmitSignedPackageAsync(id, ct);
        return Respond(result, LoanSuccessMessages.SignedPackageSubmitted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/legal/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveLegalReview(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveLegalReviewAsync(id, request.Comment, request.InternalComment, ct);
        return Respond(result, LoanSuccessMessages.LegalReviewApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/legal/revision-request")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestLegalRevision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestLegalRevisionAsync(id, request.Message, ct);
        return Respond(result, LoanSuccessMessages.LegalRevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/financial/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ApproveFinancialReview(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.ApproveFinancialReviewAsync(id, request.Comment, request.InternalComment, ct);
        return Respond(result, LoanSuccessMessages.FinancialReviewApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/financial/revision-request")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestFinancialRevision(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.RequestFinancialRevisionAsync(id, request.Message, ct);
        return Respond(result, LoanSuccessMessages.FinancialRevisionRequested, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/legal/final-uploaded")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ConfirmFinalUploaded(Guid id, CancellationToken ct)
    {
        var result = await service.ConfirmFinalContractUploadedAsync(id, ct);
        return Respond(result, LoanSuccessMessages.FinalContractUploaded, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/final/approve")]
    [Authorize(Policy = "LoanCases.CeoApprove")]
    public async Task<IActionResult> CeoApproveFinal(Guid id, [FromBody] SemanticTransitionRequest request, CancellationToken ct)
    {
        var result = await service.CeoApproveFinalAsync(id, request.Comment, ct);
        return Respond(result, LoanSuccessMessages.CeoFinalApproved, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/ceo/final/reject")]
    [Authorize(Policy = "LoanCases.CeoApprove")]
    public async Task<IActionResult> CeoRejectFinal(Guid id, [FromBody] SemanticRevisionRequest request, CancellationToken ct)
    {
        var result = await service.CeoRejectFinalAsync(id, request.Message, ct);
        return Respond(result, LoanSuccessMessages.CeoFinalRejected, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RegisterPayment(Guid id, [FromBody] RegisterLoanPaymentRequest request, CancellationToken ct)
    {
        var result = await service.RegisterPaymentAsync(id, request, ct);
        return Respond(result, LoanSuccessMessages.PaymentRegistered, HttpStatusCode.Accepted);
    }

    [HttpGet("{id:guid}/payments")]
    [Authorize]
    public async Task<IActionResult> ListPayments(Guid id, CancellationToken ct)
    {
        var result = await service.ListPaymentsAsync(id, ct);
        return Respond(result, LoanSuccessMessages.PaymentsRetrieved);
    }

    [HttpPost("{id:guid}/installments/{installmentId:guid}/mark-paid")]
    [Authorize]
    public async Task<IActionResult> MarkInstallmentPaid(
        Guid id,
        Guid installmentId,
        [FromBody] MarkLoanInstallmentPaidRequest request,
        CancellationToken ct)
    {
        var result = await service.MarkInstallmentPaidAsync(id, installmentId, request, ct);
        return Respond(result, LoanSuccessMessages.InstallmentsUpdated);
    }

    [HttpPost("{id:guid}/repayment/complete")]
    [Authorize]
    public async Task<IActionResult> CompleteRepayment(Guid id, CancellationToken ct)
    {
        var result = await service.CompleteRepaymentAsync(id, ct);
        return Respond(result, LoanSuccessMessages.RepaymentCompleted, HttpStatusCode.Accepted);
    }

    [HttpPost("{id:guid}/documents/presign")]
    [Authorize]
    public async Task<IActionResult> Presign(Guid id, [FromBody] PresignLoanUploadRequest request, CancellationToken ct)
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
