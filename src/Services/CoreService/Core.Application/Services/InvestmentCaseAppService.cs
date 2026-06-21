using Core.Application.Common;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Observability.Correlation;
using Core.Application.Authorization;
using Core.Application.Abstractions;
using Core.Application.Logging;
using Core.Application.Notifications.Sms;
using Core.Application.Mappers;
using Microsoft.EntityFrameworkCore;
using Core.Application.DTOs;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Domain.Constants;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Core.Domain.Identity;
using Core.Domain.Identity.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;


namespace Core.Application.Services;

public sealed class InvestmentCaseAppService(
    ICoreUnitOfWork unitOfWork,
    ICoreDbContext dbContext,
    ICaseStateManager stateManager,
    ICaseWorkflowOrchestrator workflowOrchestrator,
    ICaseNumberGenerator caseNumberGenerator,
    IDocumentStorage documentStorage,
    BuildingBlocks.Domain.Abstractions.IClock clock,
    IUserContext userContext,
    ICaseAuthorizationService authorizationService,
    ICaseDtoMapper caseDtoMapper,
    IUserDisplayLookup userDisplayLookup,
    IHttpContextAccessor httpContextAccessor,
    ICaseWorkflowSmsNotifier workflowSmsNotifier,
    ILogger<InvestmentCaseAppService> logger) : IInvestmentCaseAppService
{
    public async Task<Result<InvestmentCaseDto>> CreateAsync(CreateInvestmentCaseRequest request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure)
        {
            ApplicationLog.Blocked(logger, "CreateInvestmentCase", "user is not authenticated");
            return Result<InvestmentCaseDto>.Fail(authResult.Error!);
        }

        ApplicationLog.Started(logger, "CreateInvestmentCase", authResult.Value);

        if (!authorizationService.HasPermission(CasePermissions.Create))
        {
            ApplicationLog.Blocked(logger, "CreateInvestmentCase", "missing Create permission", authResult.Value);
            return Result<InvestmentCaseDto>.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var now = clock.UtcNow;
        var caseNumber = await caseNumberGenerator.GenerateAsync(cancellationToken);
        var entity = new InvestmentCase(caseNumber, authResult.Value!, request.ApplicantType);
        Company? linkedCompany = null;

        if (request.ApplicantType == ApplicantType.Company)
        {
            if (request.CompanyId is null || request.CompanyId == Guid.Empty)
                return Result<InvestmentCaseDto>.Fail(Error.Validation(ApiMessages.CompanyRequiredForCompanyApplicant));

            if (!Guid.TryParse(authResult.Value, out var userId))
                return Result<InvestmentCaseDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

            linkedCompany = await unitOfWork.Companies.FirstOrDefaultAsync(
                c => c.Id == request.CompanyId.Value,
                asNoTracking: true,
                cancellationToken);

            if (linkedCompany is null)
                return Result<InvestmentCaseDto>.Fail(Error.NotFound(ApiMessages.CompanyNotFound));

            if (linkedCompany.OwnerUserId != userId)
                return Result<InvestmentCaseDto>.Fail(Error.Forbidden(ApiMessages.CompanyAccessDenied));

            entity.AssignCompany(linkedCompany.Id);
        }

        entity.SetTitle(request.Title);

        var workflowInstanceId = await workflowOrchestrator.StartAsync(entity.Id, cancellationToken);
        entity.AttachWorkflowInstance(workflowInstanceId);

        await unitOfWork.InvestmentCases.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} created investment case {CaseId} ({CaseNumber}) as {ApplicantType}; workflow instance {WorkflowInstanceId} started",
            authResult.Value, entity.Id, entity.CaseNumber, request.ApplicantType, workflowInstanceId);

        return Result<InvestmentCaseDto>.Ok(caseDtoMapper.MapCase(entity, now, isInternalView: false, linkedCompany));
    }

    public async Task<Result<InvestmentCaseDto>> GetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure)
        {
            ApplicationLog.Blocked(logger, "GetInvestmentCase", "user is not authenticated", caseId: caseId);
            return Result<InvestmentCaseDto>.Fail(authResult.Error!);
        }

        ApplicationLog.Started(logger, "GetInvestmentCase", authResult.Value, caseId);

        var detail = await unitOfWork.InvestmentCases.GetDetailProjectionAsync(
            caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (detail is null)
        {
            ApplicationLog.Blocked(logger, "GetInvestmentCase", "case not found or access denied", authResult.Value, caseId);
            return Result<InvestmentCaseDto>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        ApplicationLog.Completed(logger,
            "User {UserId} loaded case {CaseId} ({CaseNumber}) — phase {Phase}, status {Status}",
            authResult.Value, detail.Id, detail.CaseNumber, detail.CurrentPhase, detail.CurrentStatus);

        ApplicantContactDto? applicantContact = null;
        if (!authorizationService.IsInternalUser)
            applicantContact = await LoadApplicantContactAsync(authResult.Value!, cancellationToken);

        if (authorizationService.IsInternalUser
            && (string.IsNullOrWhiteSpace(detail.ApplicantFullName) || string.IsNullOrWhiteSpace(detail.ApplicantPhoneNumber)))
        {
            var lookup = await userDisplayLookup.GetByIdsAsync([detail.ApplicantUserId], cancellationToken);
            if (lookup.TryGetValue(detail.ApplicantUserId, out var applicantDisplay))
            {
                detail = detail with
                {
                    ApplicantFullName = detail.ApplicantFullName ?? applicantDisplay.FullName,
                    ApplicantPhoneNumber = detail.ApplicantPhoneNumber ?? applicantDisplay.PhoneNumber
                };
            }
        }

        return Result<InvestmentCaseDto>.Ok(
            caseDtoMapper.MapFromDetailProjection(
                detail,
                clock.UtcNow,
                authorizationService.IsInternalUser,
                applicantContact));
    }

    public async Task<Result<InvestmentCaseDto>> UpdateTitleAsync(
        Guid caseId,
        UpdateCaseTitleRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure)
            return Result<InvestmentCaseDto>.Fail(authResult.Error!);

        var isInternal = authorizationService.IsInternalUser;
        var exists = await dbContext.InvestmentCases
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == caseId && (isInternal || x.ApplicantUserId == authResult.Value),
                cancellationToken);

        if (!exists)
            return Result<InvestmentCaseDto>.Fail(Error.NotFound(ApiMessages.CaseNotFound));

        var title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        var rows = await dbContext.InvestmentCases.SetTitleAsync(caseId, title, clock.UtcNow, cancellationToken);

        if (rows == 0)
            return Result<InvestmentCaseDto>.Fail(Error.NotFound(ApiMessages.CaseNotFound));

        return await GetAsync(caseId, cancellationToken);
    }

    public async Task<Result> UpdateDataEntry1Async(Guid caseId, UpdateDataEntry1Request request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "UpdateDataEntry1", authResult.Value, caseId);

        var isAdmin = userContext.Roles.Contains(UserRoleClaims.Admin);
        if (!userContext.Roles.Contains(UserRoleClaims.Applicant) && !isAdmin)
        {
            ApplicationLog.Blocked(logger, "UpdateDataEntry1", "only applicants may edit data entry 1", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var caseStatus = await dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => x.Id == caseId && (isAdmin || x.ApplicantUserId == authResult.Value))
            .Select(x => x.CurrentStatus)
            .FirstOrDefaultAsync(cancellationToken);

        if (caseStatus == default)
        {
            ApplicationLog.Blocked(logger, "UpdateDataEntry1", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        if (caseStatus is not (CaseStatus.Draft or CaseStatus.DataEntry1))
        {
            ApplicationLog.Blocked(logger, "UpdateDataEntry1", $"case status is {caseStatus}", authResult.Value, caseId);
            return Result.Fail(Error.Conflict(ApiMessages.DataEntry1NotEditable));
        }

        var identity = await ResolveApplicantIdentityAsync(authResult.Value!, cancellationToken);
        if (identity.IsFailure)
            return Result.Fail(identity.Error!);

        var (fullName, email) = identity.Value;

        var dataEntry = await dbContext.InvestmentCaseApplicantProfiles
            .FirstOrDefaultAsync(x => x.CaseId == caseId, cancellationToken);

        if (dataEntry is null)
        {
            dataEntry = new InvestmentCaseApplicantProfile(
                caseId,
                fullName,
                request.BusinessStage,
                email,
                request.RequestedAmount);
            await dbContext.InvestmentCaseApplicantProfiles.AddAsync(dataEntry, cancellationToken);
        }
        else
        {
            dataEntry.Update(fullName, request.BusinessStage, email, request.RequestedAmount);
        }

        await dbContext.InvestmentCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} saved data entry 1 on case {CaseId} (representative: {Representative})",
            authResult.Value, caseId, fullName);

        return Result.Ok();
    }

    public async Task<Result> UpdateDataEntry2Async(Guid caseId, UpdateDataEntry2Request request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "UpdateDataEntry2", authResult.Value, caseId);

        var isAdmin = userContext.Roles.Contains(UserRoleClaims.Admin);
        if (!userContext.Roles.Contains(UserRoleClaims.Applicant) && !isAdmin)
        {
            ApplicationLog.Blocked(logger, "UpdateDataEntry2", "only applicants may edit data entry 2", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var caseStatus = await dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => x.Id == caseId && (isAdmin || x.ApplicantUserId == authResult.Value))
            .Select(x => x.CurrentStatus)
            .FirstOrDefaultAsync(cancellationToken);

        if (caseStatus == default)
        {
            ApplicationLog.Blocked(logger, "UpdateDataEntry2", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        if (caseStatus is not CaseStatus.DataEntry2)
        {
            ApplicationLog.Blocked(logger, "UpdateDataEntry2", $"case status is {caseStatus}", authResult.Value, caseId);
            return Result.Fail(Error.Conflict(ApiMessages.DataEntry2NotEditable));
        }

        var dataEntry = await dbContext.InvestmentCaseAttractionBases
            .FirstOrDefaultAsync(x => x.CaseId == caseId, cancellationToken);

        if (dataEntry is null)
        {
            dataEntry = new InvestmentCaseAttractionBasis(caseId, request.InvestmentAttractionBasis);
            await dbContext.InvestmentCaseAttractionBases.AddAsync(dataEntry, cancellationToken);
        }
        else
        {
            dataEntry.Update(request.InvestmentAttractionBasis);
        }

        await dbContext.InvestmentCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} saved data entry 2 on case {CaseId}",
            authResult.Value, caseId);

        return Result.Ok();
    }

    public async Task<Result> UpdateFinancialWorksheetAsync(Guid caseId, UpdateFinancialWorksheetRequest request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "UpdateFinancialWorksheet", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.ManageFinancialWorksheet))
        {
            ApplicationLog.Blocked(logger, "UpdateFinancialWorksheet", "missing ManageFinancialWorksheet permission", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "UpdateFinancialWorksheet", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        if (entity.CurrentStatus is not CaseStatus.WaitingFinancialWorksheet)
        {
            ApplicationLog.Blocked(logger, "UpdateFinancialWorksheet", $"case status is {entity.CurrentStatus}", authResult.Value, caseId);
            return Result.Fail(Error.Conflict(ApiMessages.FinancialWorksheetNotEditable));
        }

        var wasNewWorksheet = entity.FinancialWorksheet is null;
        var worksheet = entity.UpsertFinancialWorksheet(
            request.BankName,
            request.Iban,
            request.ApprovedAmount ?? 0m,
            request.PaymentSchedule ?? string.Empty,
            request.Notes);

        if (wasNewWorksheet)
            await dbContext.FinancialWorksheets.AddAsync(worksheet, cancellationToken);

        UntrackCaseRoot(entity);
        await dbContext.InvestmentCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} updated financial worksheet on case {CaseId} ({CaseNumber})",
            authResult.Value, caseId, entity.CaseNumber);

        return Result.Ok();
    }

    public async Task<Result> SubmitDataEntry1Async(Guid caseId, string? comment, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.Submit, comment, ct);

    public async Task<Result> ApproveDataEntry1Async(Guid caseId, string? comment, string? internalComment, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.Approve, comment, ct, internalComment);

    public async Task<Result> RequestDataEntry1RevisionAsync(Guid caseId, string message, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.RequestRevision, message, ct);

    public async Task<Result> SubmitDataEntry2Async(Guid caseId, string? comment, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.Submit, comment, ct);

    public async Task<Result> ApproveDataEntry2Async(Guid caseId, string? comment, string? internalComment, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.Approve, comment, ct, internalComment);

    public async Task<Result> RequestDataEntry2RevisionAsync(Guid caseId, string message, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.RequestRevision, message, ct);

    public async Task<Result> ApproveInitialValuationAsync(Guid caseId, string? comment, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.ManageValuations))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return await ApplyTransitionAsync(caseId, WorkflowAction.Approve, comment, ct);
    }

    public async Task<Result> ApproveSecondaryValuationAsync(Guid caseId, string? comment, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.ManageValuations))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return await ApplyTransitionAsync(caseId, WorkflowAction.Approve, comment, ct);
    }

    /// <summary>
    /// Legacy entry point when only <paramref name="s3Key"/> is known. Prefer presign → PUT storage → <see cref="ConfirmDocumentUploadedAsync"/>.
    /// </summary>
    public async Task<Result> UploadPreliminaryContractAsync(Guid caseId, string s3Key, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.ManageContracts))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var result = await ConfirmDocumentUploadedAsync(caseId, s3Key, null, ct);
        return result.IsFailure ? Result.Fail(result.Error!) : Result.Ok();
    }

    public async Task<Result> ApprovePreliminaryContractAsync(Guid caseId, string? comment, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.Approve, comment, ct);

    public async Task<Result> RequestPreliminaryContractRevisionAsync(Guid caseId, string message, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.RequestRevision, message, ct);

    public async Task<Result> FinalizeContractDraftAsync(Guid caseId, string? comment, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.ManageContracts))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return await ApplyTransitionAsync(caseId, WorkflowAction.FinalizeContractDraft, comment, ct);
    }

    public async Task<Result> ConfirmSignatureAsync(Guid caseId, string? comment, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.ManageContracts))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return await ApplyTransitionAsync(caseId, WorkflowAction.ConfirmSignature, comment, ct);
    }

    /// <summary>
    /// Legacy entry point when only <paramref name="s3Key"/> is known. Prefer presign → PUT storage → <see cref="ConfirmDocumentUploadedAsync"/>.
    /// </summary>
    public async Task<Result> UploadSignedContractAsync(Guid caseId, string s3Key, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.ManageContracts))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var result = await ConfirmDocumentUploadedAsync(caseId, s3Key, null, ct);
        return result.IsFailure ? Result.Fail(result.Error!) : Result.Ok();
    }

    public async Task<Result> SubmitFinancialWorksheetAsync(Guid caseId, string? comment, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.ManageFinancialWorksheet))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return await ApplyTransitionAsync(caseId, WorkflowAction.SubmitFinancialWorksheet, comment, ct);
    }

    public async Task<Result> ApproveFinancialWorksheetAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.ManageFinancialWorksheet))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return await ApplyTransitionAsync(caseId, WorkflowAction.ApproveFinancialWorksheet, comment, ct, internalComment);
    }

    public async Task<Result> RequestFinancialWorksheetRevisionAsync(Guid caseId, string message, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.ManageFinancialWorksheet))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return await ApplyTransitionAsync(caseId, WorkflowAction.RequestRevision, message, ct);
    }

    public async Task<Result> ApproveCeoAsync(Guid caseId, string? comment, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.CeoApprove))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return await ApplyTransitionAsync(caseId, WorkflowAction.Approve, comment, ct);
    }

    public async Task<Result> RequestCeoRevisionAsync(Guid caseId, string message, CancellationToken ct)
    {
        if (!authorizationService.HasPermission(CasePermissions.CeoApprove))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return await ApplyTransitionAsync(caseId, WorkflowAction.RequestRevision, message, ct);
    }

    public async Task<Result> ConfirmPaymentAsync(Guid caseId, Guid paymentId, CancellationToken ct)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "ConfirmPayment", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.ManagePayments))
        {
            ApplicationLog.Blocked(logger, "ConfirmPayment", "missing ManagePayments permission", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var caseCtx = await GetPaymentCaseContextAsync(caseId, ct);
        if (caseCtx is null)
        {
            ApplicationLog.Blocked(logger, "ConfirmPayment", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var payment = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.CaseId == caseId, ct);
        if (payment is null)
        {
            ApplicationLog.Blocked(logger, "ConfirmPayment", $"payment {paymentId} not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.PaymentNotFound));
        }

        payment.Update(
            payment.Amount,
            payment.PaymentDate,
            payment.TransactionNumber,
            payment.ReceiptS3Key,
            payment.Notes,
            payment.Method,
            PaymentStatus.Completed);

        await dbContext.InvestmentCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var autoComplete = await TryAutoCompleteCaseAfterPaymentsAsync(caseId, authResult.Value!, ct);
        if (autoComplete.IsFailure)
            return autoComplete;

        var statusAfter = await dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => x.CurrentStatus)
            .FirstAsync(ct);

        ApplicationLog.Completed(logger,
            "User {UserId} confirmed payment {PaymentId} on case {CaseId} ({CaseNumber}); case status is now {Status}",
            authResult.Value, paymentId, caseId, caseCtx.CaseNumber, statusAfter);

        return Result.Ok();
    }

    public async Task<Result> CancelPaymentAsync(Guid caseId, Guid paymentId, CancellationToken ct)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "CancelPayment", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.ManagePayments))
        {
            ApplicationLog.Blocked(logger, "CancelPayment", "missing ManagePayments permission", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var caseCtx = await GetPaymentCaseContextAsync(caseId, ct);
        if (caseCtx is null)
        {
            ApplicationLog.Blocked(logger, "CancelPayment", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var payment = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.CaseId == caseId, ct);
        if (payment is null)
        {
            ApplicationLog.Blocked(logger, "CancelPayment", $"payment {paymentId} not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.PaymentNotFound));
        }

        payment.Update(
            payment.Amount,
            payment.PaymentDate,
            payment.TransactionNumber,
            payment.ReceiptS3Key,
            payment.Notes,
            payment.Method,
            PaymentStatus.Cancelled);

        await dbContext.InvestmentCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);

        ApplicationLog.Completed(logger,
            "User {UserId} cancelled payment {PaymentId} on case {CaseId} ({CaseNumber})",
            authResult.Value, paymentId, caseId, caseCtx.CaseNumber);

        return Result.Ok();
    }

    public async Task<Result> RejectAsync(Guid caseId, string reason, CancellationToken ct)
    {
        return await ApplyTransitionAsync(caseId, WorkflowAction.Reject, $"Rejected: {reason}", ct);
    }

    public async Task<Result> CancelAsync(Guid caseId, string reason, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.Cancel, $"Cancelled: {reason}", ct);

    public async Task<Result> ArchiveAsync(Guid caseId, string reason, CancellationToken ct)
        => await ApplyTransitionAsync(caseId, WorkflowAction.Archive, $"Archived: {reason}", ct);

    private async Task<Result> ApplyTransitionAsync(
        Guid caseId,
        WorkflowAction action,
        string? comment,
        CancellationToken ct,
        string? internalComment = null)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure)
        {
            ApplicationLog.Blocked(logger, $"Workflow:{action}", "user is not authenticated", caseId: caseId);
            return Result.Fail(authResult.Error!);
        }

        if (action == WorkflowAction.RequestRevision && string.IsNullOrWhiteSpace(comment))
            return Result.Fail(Error.Validation(ApiMessages.RevisionMessageRequired));

        var actorRole = ResolveActorRole();
        ApplicationLog.Started(logger, $"Workflow:{action}", authResult.Value, caseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedForTransitionAsync(
            caseId, authResult.Value!, authorizationService.IsInternalUser, ct);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, $"Workflow:{action}", "case not found or access denied", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var statusBefore = entity.CurrentStatus;
        var phaseBefore = entity.CurrentPhase;
        var historyCountBefore = entity.WorkflowHistory.Count;
        var commentsCountBefore = entity.Comments.Count;

        var correlationId = ResolveCorrelationGuid(httpContextAccessor.HttpContext);
        var transition = await stateManager.TransitionAsync(entity, action, authResult.Value!, actorRole, comment, correlationId);
        if (transition.IsFailure)
        {
            ApplicationLog.Blocked(logger, $"Workflow:{action}",
                transition.Error?.Message ?? "transition rejected by state machine",
                authResult.Value, caseId);
            return transition;
        }

        if (!string.IsNullOrWhiteSpace(internalComment) && SupportsInternalApproveComment(action, statusBefore))
        {
            if (!authorizationService.HasPermission(CasePermissions.CreateInternalComment))
            {
                ApplicationLog.Blocked(logger, $"Workflow:{action}", "cannot create internal comment", authResult.Value, caseId);
                return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
            }

            entity.AddDiscussionComment(
                phaseBefore,
                authResult.Value!,
                actorRole,
                internalComment,
                isRevisionRequest: false,
                isInternal: true);
        }

        if (entity.WorkflowHistory.Count > historyCountBefore)
        {
            var persistResult = await PersistCaseTransitionAsync(entity, commentsCountBefore, ct);
            if (persistResult.IsFailure)
                return persistResult;

            await NotifyWorkflowSmsSafeAsync(
                entity.Id,
                entity.ApplicantUserId,
                entity.CaseNumber,
                statusBefore,
                entity.CurrentStatus,
                action,
                ct);
        }

        try
        {
            await workflowOrchestrator.SignalAsync(
                caseId,
                WorkflowSignals.StatusChanged,
                payload: null,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Elsa workflow signal failed for case {CaseId}; domain transition is already saved.",
                caseId);
        }

        ApplicationLog.Completed(logger,
            "User {UserId} (role {Role}) applied {Action} on case {CaseId} ({CaseNumber}): {PhaseBefore}/{StatusBefore} → {PhaseAfter}/{StatusAfter}",
            authResult.Value, actorRole, action, caseId, entity.CaseNumber,
            phaseBefore, statusBefore, entity.CurrentPhase, entity.CurrentStatus);

        return Result.Ok();
    }

    private async Task NotifyWorkflowSmsSafeAsync(
        Guid caseId,
        string applicantUserId,
        string caseNumber,
        CaseStatus from,
        CaseStatus to,
        WorkflowAction action,
        CancellationToken ct)
    {
        try
        {
            await workflowSmsNotifier.NotifyStatusChangeAsync(
                caseId, applicantUserId, caseNumber, from, to, action, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workflow SMS notification failed for case {CaseId}", caseId);
        }
    }

    private static Guid ResolveCorrelationGuid(HttpContext? httpContext)
    {
        var raw = httpContext?.Items[CorrelationContext.ItemKey]?.ToString()
                  ?? httpContext?.Request.Headers[CorrelationContext.HeaderName].ToString()
                  ?? httpContext?.TraceIdentifier;

        if (string.IsNullOrWhiteSpace(raw))
            return Guid.NewGuid();

        if (Guid.TryParse(raw, out var parsed))
            return parsed;

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
        var guidBytes = bytes.Take(16).ToArray();
        return new Guid(guidBytes);
    }

    private string ResolveActorRole()
    {
        if (userContext.Roles.Contains(UserRoleClaims.Admin)) return UserRoleClaims.Admin;
        if (userContext.Roles.Contains(UserRoleClaims.Ceo) ||
            userContext.Roles.Contains("CEO", StringComparer.OrdinalIgnoreCase))
            return UserRoleClaims.Ceo;
        if (userContext.Roles.Contains(UserRoleClaims.InvestmentManager)) return UserRoleClaims.InvestmentManager;
        if (userContext.Roles.Contains(UserRoleClaims.InvestmentExpert)) return UserRoleClaims.InvestmentExpert;
        if (userContext.Roles.Contains(UserRoleClaims.LegalManager)) return UserRoleClaims.LegalManager;
        if (userContext.Roles.Contains(UserRoleClaims.LegalExpert) ||
            userContext.Roles.Contains(UserRoleClaims.LegalUnit, StringComparer.OrdinalIgnoreCase))
            return UserRoleClaims.LegalExpert;
        if (userContext.Roles.Contains(UserRoleClaims.FinancialManager)) return UserRoleClaims.FinancialManager;
        if (userContext.Roles.Contains(UserRoleClaims.FinancialExpert) ||
            userContext.Roles.Contains(UserRoleClaims.FinancialUnit, StringComparer.OrdinalIgnoreCase))
            return UserRoleClaims.FinancialExpert;
        if (userContext.Roles.Contains(UserRoleClaims.TechnicalManager)) return UserRoleClaims.TechnicalManager;
        if (userContext.Roles.Contains(UserRoleClaims.TechnicalExpert)) return UserRoleClaims.TechnicalExpert;
        if (userContext.Roles.Contains(UserRoleClaims.Applicant)) return UserRoleClaims.Applicant;
        return userContext.Roles.FirstOrDefault() ?? string.Empty;
    }

    public async Task<Result> RecordValuationAsync(Guid caseId, RecordValuationRequest request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "RecordValuation", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.ManageValuations))
        {
            ApplicationLog.Blocked(logger, "RecordValuation", "missing ManageValuations permission", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var entity = await unitOfWork.InvestmentCases.GetScopedForTransitionAsync(
            caseId, authResult.Value!, isInternalUser: true, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "RecordValuation", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var requiredStatus = request.Type == ValuationType.Primary ? CaseStatus.InitialValuation : CaseStatus.SecondaryValuation;
        if (entity.CurrentStatus != requiredStatus)
        {
            ApplicationLog.Blocked(logger, "RecordValuation",
                $"case status is {entity.CurrentStatus}, expected {requiredStatus}", authResult.Value, caseId);
            return Result.Fail(Error.Conflict(string.Format(ApiMessages.ValuationStatusMismatch, requiredStatus)));
        }

        await dbContext.CaseValuations.AddAsync(
            new InvestmentCaseValuation(caseId, request.Type, request.Amount, request.Notes ?? string.Empty, authResult.Value!),
            cancellationToken);
        await dbContext.InvestmentCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} recorded {ValuationType} valuation {Amount} on case {CaseId} ({CaseNumber})",
            authResult.Value, request.Type, request.Amount, caseId, entity.CaseNumber);

        return Result.Ok();
    }

    public async Task<Result<CasePaymentsDto>> GetPaymentsAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure)
            return Result<CasePaymentsDto>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "GetPayments", authResult.Value, caseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(
            caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "GetPayments", "case not found", authResult.Value, caseId);
            return Result<CasePaymentsDto>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var dto = caseDtoMapper.MapPayments(entity);
        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} payment(s) for case {CaseId} ({CaseNumber})",
            authResult.Value, dto.Payments.Count, caseId, entity.CaseNumber);

        return Result<CasePaymentsDto>.Ok(dto);
    }

    public async Task<Result> RecordPaymentAsync(Guid caseId, RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "RecordPayment", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.ManagePayments))
        {
            ApplicationLog.Blocked(logger, "RecordPayment", "missing ManagePayments permission", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var caseCtx = await GetPaymentCaseContextAsync(caseId, cancellationToken);
        if (caseCtx is null)
        {
            ApplicationLog.Blocked(logger, "RecordPayment", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        if (caseCtx.Status != CaseStatus.WaitingPayment)
        {
            ApplicationLog.Blocked(logger, "RecordPayment", $"case status is {caseCtx.Status}", authResult.Value, caseId);
            var message = caseCtx.Status == CaseStatus.WaitingCeoApproval
                ? ApiMessages.CeoApprovalRequiredForPayment
                : ApiMessages.PaymentsOnlyInWaitingPayment;
            return Result.Fail(Error.Conflict(message));
        }

        var payment = new InvestmentCasePayment(
            caseId,
            request.Amount,
            request.PaymentDate,
            request.TransactionNumber,
            request.ReceiptS3Key,
            request.Notes,
            request.Method,
            request.Status,
            authResult.Value!);

        await dbContext.PaymentRecords.AddAsync(payment, cancellationToken);
        await dbContext.InvestmentCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var autoComplete = await TryAutoCompleteCaseAfterPaymentsAsync(caseId, authResult.Value!, cancellationToken);
        if (autoComplete.IsFailure)
            return autoComplete;

        ApplicationLog.Completed(logger,
            "User {UserId} recorded payment {Amount} ({Status}) on case {CaseId} ({CaseNumber})",
            authResult.Value, request.Amount, request.Status, caseId, caseCtx.CaseNumber);

        return Result.Ok();
    }

    public async Task<Result> UpdatePaymentAsync(Guid caseId, Guid paymentId, UpdatePaymentRequest request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "UpdatePayment", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.ManagePayments))
        {
            ApplicationLog.Blocked(logger, "UpdatePayment", "missing ManagePayments permission", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var caseCtx = await GetPaymentCaseContextAsync(caseId, cancellationToken);
        if (caseCtx is null)
        {
            ApplicationLog.Blocked(logger, "UpdatePayment", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var payment = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.CaseId == caseId, cancellationToken);
        if (payment is null)
        {
            ApplicationLog.Blocked(logger, "UpdatePayment", $"payment {paymentId} not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.PaymentNotFound));
        }

        payment.Update(
            request.Amount,
            request.PaymentDate,
            request.TransactionNumber,
            request.ReceiptS3Key,
            request.Notes,
            request.Method,
            request.Status);

        await dbContext.InvestmentCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var autoComplete = await TryAutoCompleteCaseAfterPaymentsAsync(caseId, authResult.Value!, cancellationToken);
        if (autoComplete.IsFailure)
            return autoComplete;

        ApplicationLog.Completed(logger,
            "User {UserId} updated payment {PaymentId} on case {CaseId} ({CaseNumber}) to status {Status}",
            authResult.Value, paymentId, caseId, caseCtx.CaseNumber, request.Status);

        return Result.Ok();
    }

    public async Task<Result> DeletePaymentAsync(Guid caseId, Guid paymentId, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "DeletePayment", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.ManagePayments))
        {
            ApplicationLog.Blocked(logger, "DeletePayment", "missing ManagePayments permission", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var caseCtx = await GetPaymentCaseContextAsync(caseId, cancellationToken);
        if (caseCtx is null)
        {
            ApplicationLog.Blocked(logger, "DeletePayment", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var payment = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.CaseId == caseId, cancellationToken);
        if (payment is null)
        {
            ApplicationLog.Blocked(logger, "DeletePayment", $"payment {paymentId} not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.PaymentNotFound));
        }

        dbContext.PaymentRecords.Remove(payment);
        await dbContext.InvestmentCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} deleted payment {PaymentId} from case {CaseId} ({CaseNumber})",
            authResult.Value, paymentId, caseId, caseCtx.CaseNumber);

        return Result.Ok();
    }

    public async Task<Result<IEnumerable<CaseCommentDto>>> GetCommentsAsync(Guid caseId, bool includeInternal, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<IEnumerable<CaseCommentDto>>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "GetComments", authResult.Value, caseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "GetComments", "case not found", authResult.Value, caseId);
            return Result<IEnumerable<CaseCommentDto>>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var canViewInternal = includeInternal && authorizationService.HasPermission(CasePermissions.ViewInternalComments);
        var isInternalView = authorizationService.IsInternalUser;

        var visibleComments = entity.Comments
            .Where(x => (isInternalView && canViewInternal) || !x.IsInternal)
            .ToList();

        var userLookup = isInternalView
            ? await userDisplayLookup.GetByIdsAsync(visibleComments.Select(x => x.SenderUserId), cancellationToken)
            : null;

        var comments = visibleComments
            .Select(c => caseDtoMapper.MapComment(
                c,
                userLookup is null ? null : userDisplayLookup.ResolveFullName(userLookup, c.SenderUserId)))
            .OrderBy(x => x.CreatedAt)
            .ToList();

        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} comment(s) for case {CaseId} (includeInternal={IncludeInternal})",
            authResult.Value, comments.Count, caseId, includeInternal);

        return Result<IEnumerable<CaseCommentDto>>.Ok(comments);
    }

    public async Task<Result> AddCommentAsync(Guid caseId, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "AddComment", authResult.Value, caseId);

        var isInternalRequested = request.IsInternal;
        if (isInternalRequested && !authorizationService.HasPermission(CasePermissions.CreateInternalComment))
        {
            ApplicationLog.Blocked(logger, "AddComment", "cannot create internal comment", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        if (isInternalRequested)
        {
            ApplicationLog.Blocked(logger, "AddComment", "internal comments only via approve endpoints", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.FreeformCommentNotAllowed));
        }

        var caseMeta = await GetCaseMetaForChildWriteAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (caseMeta is null)
        {
            ApplicationLog.Blocked(logger, "AddComment", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        if (!IsFreeformCommentAllowed(caseMeta.Status, request.Phase))
        {
            ApplicationLog.Blocked(logger, "AddComment", "freeform comment not allowed in current stage", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.FreeformCommentNotAllowed));
        }

        await dbContext.CaseComments.AddAsync(
            new InvestmentCaseComment(
                caseId,
                request.Phase,
                authResult.Value!,
                senderRole: string.Join(",", userContext.Roles),
                request.Message,
                isRevisionRequest: false,
                isInternal: false,
                parentId: request.ParentId),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} added {Visibility} comment on case {CaseId} ({CaseNumber}) in phase {Phase}",
            authResult.Value, "public", caseId, caseMeta.CaseNumber, request.Phase);

        return Result.Ok();
    }

    public async Task<Result> AddCommentAttachmentAsync(Guid caseId, Guid commentId, string s3Key, string fileName, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "AddCommentAttachment", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.UploadCommentAttachments))
        {
            ApplicationLog.Blocked(logger, "AddCommentAttachment", "missing UploadCommentAttachments permission", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var caseMeta = await GetCaseMetaForChildWriteAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (caseMeta is null)
        {
            ApplicationLog.Blocked(logger, "AddCommentAttachment", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var commentExists = await dbContext.CaseComments
            .AsNoTracking()
            .AnyAsync(x => x.Id == commentId && x.CaseId == caseId, cancellationToken);
        if (!commentExists)
        {
            ApplicationLog.Blocked(logger, "AddCommentAttachment", $"comment {commentId} not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CommentNotFound));
        }

        var attachmentValidation = ValidateCommentAttachmentKey(caseMeta.CaseNumber, commentId, s3Key, fileName);
        if (attachmentValidation.IsFailure) return attachmentValidation;

        var exists = await documentStorage.ExistsAsync(s3Key, cancellationToken);
        if (!exists)
        {
            ApplicationLog.Blocked(logger, "AddCommentAttachment", "file not found in storage", authResult.Value, caseId);
            return Result.Fail(Error.Conflict(ApiMessages.UploadedFileNotFound));
        }

        await dbContext.CaseCommentAttachments.AddAsync(
            new InvestmentCaseCommentAttachment(commentId, s3Key, fileName),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} attached file {FileName} to comment {CommentId} on case {CaseId}",
            authResult.Value, fileName, commentId, caseId);

        return Result.Ok();
    }

    public async Task<Result<IEnumerable<CaseDocumentDto>>> GetDocumentsAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<IEnumerable<CaseDocumentDto>>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "GetDocuments", authResult.Value, caseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "GetDocuments", "case not found", authResult.Value, caseId);
            return Result<IEnumerable<CaseDocumentDto>>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var documents = entity.Documents.ToList();
        var userLookup = authorizationService.IsInternalUser
            ? await userDisplayLookup.GetByIdsAsync(documents.Select(x => x.UploadedByUserId), cancellationToken)
            : null;

        var docs = documents
            .Select(d => caseDtoMapper.MapDocument(
                d,
                userLookup is null ? null : userDisplayLookup.ResolveFullName(userLookup, d.UploadedByUserId)))
            .OrderByDescending(x => x.UploadedAt)
            .ToList();

        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} document(s) for case {CaseId} ({CaseNumber})",
            authResult.Value, docs.Count, caseId, entity.CaseNumber);

        return Result<IEnumerable<CaseDocumentDto>>.Ok(docs);
    }

    public async Task<Result<IEnumerable<CaseDocumentDto>>> GetDocumentsLatestAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<IEnumerable<CaseDocumentDto>>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "GetDocumentsLatest", authResult.Value, caseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "GetDocumentsLatest", "case not found", authResult.Value, caseId);
            return Result<IEnumerable<CaseDocumentDto>>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var latestDocuments = SelectLatestDocuments(entity.Documents).ToList();
        var userLookup = authorizationService.IsInternalUser
            ? await userDisplayLookup.GetByIdsAsync(latestDocuments.Select(x => x.UploadedByUserId), cancellationToken)
            : null;

        var latest = latestDocuments
            .Select(d => caseDtoMapper.MapDocument(
                d,
                userLookup is null ? null : userDisplayLookup.ResolveFullName(userLookup, d.UploadedByUserId)))
            .OrderBy(x => (int)x.DocumentType)
            .ToList();

        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} latest document(s) for case {CaseId} ({CaseNumber})",
            authResult.Value, latest.Count, caseId, entity.CaseNumber);

        return Result<IEnumerable<CaseDocumentDto>>.Ok(latest);
    }

    public async Task<Result<CaseDocumentTypeVersionsDto>> GetDocumentVersionsAsync(
        Guid caseId,
        DocumentType documentType,
        CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<CaseDocumentTypeVersionsDto>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "GetDocumentVersions", authResult.Value, caseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "GetDocumentVersions", "case not found", authResult.Value, caseId);
            return Result<CaseDocumentTypeVersionsDto>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var userLookup = authorizationService.IsInternalUser
            ? await userDisplayLookup.GetByIdsAsync(entity.Documents.Select(x => x.UploadedByUserId), cancellationToken)
            : null;

        var group = BuildDocumentTypeVersions(
            entity.Documents,
            documentType,
            d => caseDtoMapper.MapDocument(
                d,
                userLookup is null ? null : userDisplayLookup.ResolveFullName(userLookup, d.UploadedByUserId)));

        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} version(s) of {DocumentType} for case {CaseId}",
            authResult.Value, group.Versions.Count, documentType, caseId);

        return Result<CaseDocumentTypeVersionsDto>.Ok(group);
    }

    public async Task<Result<IEnumerable<CaseDocumentTypeVersionsDto>>> GetDocumentVersionGroupsAsync(
        Guid caseId,
        string? scope,
        CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<IEnumerable<CaseDocumentTypeVersionsDto>>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "GetDocumentVersionGroups", authResult.Value, caseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "GetDocumentVersionGroups", "case not found", authResult.Value, caseId);
            return Result<IEnumerable<CaseDocumentTypeVersionsDto>>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var scopedTypes = DocumentVersionScopes.ResolveTypes(scope);
        var typesOnCase = entity.Documents.Select(d => d.DocumentType).Distinct().ToList();
        var types = scopedTypes.Count > 0
            ? scopedTypes.Where(typesOnCase.Contains).ToList()
            : typesOnCase.OrderBy(t => (int)t).ToList();

        var userLookup = authorizationService.IsInternalUser
            ? await userDisplayLookup.GetByIdsAsync(entity.Documents.Select(x => x.UploadedByUserId), cancellationToken)
            : null;

        var groups = types
            .Select(t => BuildDocumentTypeVersions(
                entity.Documents,
                t,
                d => caseDtoMapper.MapDocument(
                    d,
                    userLookup is null ? null : userDisplayLookup.ResolveFullName(userLookup, d.UploadedByUserId))))
            .Where(g => g.Versions.Count > 0)
            .OrderBy(g => (int)g.DocumentType)
            .ToList();

        ApplicationLog.Completed(logger,
            "User {UserId} loaded version groups ({Count} type(s), scope={Scope}) for case {CaseId}",
            authResult.Value, groups.Count, scope ?? DocumentVersionScopes.All, caseId);

        return Result<IEnumerable<CaseDocumentTypeVersionsDto>>.Ok(groups);
    }

    public async Task<Result<IEnumerable<CaseWorkflowHistoryDto>>> GetHistoryAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<IEnumerable<CaseWorkflowHistoryDto>>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "GetWorkflowHistory", authResult.Value, caseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "GetWorkflowHistory", "case not found", authResult.Value, caseId);
            return Result<IEnumerable<CaseWorkflowHistoryDto>>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var userLookup = await userDisplayLookup.GetByIdsAsync(
            entity.WorkflowHistory.Select(x => x.ChangedByUserId),
            cancellationToken);

        var history = entity.WorkflowHistory
            .Select(h => caseDtoMapper.MapHistory(
                h,
                userDisplayLookup.ResolveFullName(userLookup, h.ChangedByUserId)))
            .OrderBy(x => x.CreatedAt)
            .ToList();

        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} workflow history entry(ies) for case {CaseId} ({CaseNumber})",
            authResult.Value, history.Count, caseId, entity.CaseNumber);

        return Result<IEnumerable<CaseWorkflowHistoryDto>>.Ok(history);
    }

    public async Task<Result> UpsertEvaluationAsync(Guid caseId, CaseEvaluationUpsertRequest request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "UpsertEvaluation", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.UpsertEvaluations))
        {
            ApplicationLog.Blocked(logger, "UpsertEvaluation", "missing UpsertEvaluations permission", authResult.Value, caseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(caseId, authResult.Value!, isInternalUser: true, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "UpsertEvaluation", "case not found", authResult.Value, caseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var evaluation = entity.Evaluations.FirstOrDefault(x => x.Phase == request.Phase && x.ReviewerUserId == userContext.UserId);
        if (evaluation is null)
        {
            evaluation = new InvestmentCaseEvaluation(caseId, request.Phase, authResult.Value!, "Reviewer", request.Notes);
            entity.Evaluations.Add(evaluation);
        }
        else
        {
            evaluation.UpdateNotes(request.Notes);
        }

        var items = request.Items.Select(x => new InvestmentCaseEvaluationItem(evaluation.Id, x.Title, x.IsApproved, x.Comment)).ToList();
        evaluation.SetItems(items);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} saved evaluation for phase {Phase} on case {CaseId} ({CaseNumber}) with {ItemCount} item(s)",
            authResult.Value, request.Phase, caseId, entity.CaseNumber, request.Items.Count);

        return Result.Ok();
    }

    public async Task<Result<IEnumerable<CaseEvaluationDto>>> GetEvaluationsAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<IEnumerable<CaseEvaluationDto>>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "GetEvaluations", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.ViewEvaluations))
        {
            ApplicationLog.Blocked(logger, "GetEvaluations", "missing ViewEvaluations permission", authResult.Value, caseId);
            return Result<IEnumerable<CaseEvaluationDto>>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var entity = await unitOfWork.InvestmentCases.GetScopedAsync(caseId, authResult.Value!, isInternalUser: true, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "GetEvaluations", "case not found", authResult.Value, caseId);
            return Result<IEnumerable<CaseEvaluationDto>>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var evaluations = entity.Evaluations
            .Select(caseDtoMapper.MapEvaluation)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} evaluation(s) for case {CaseId}",
            authResult.Value, evaluations.Count, caseId);

        return Result<IEnumerable<CaseEvaluationDto>>.Ok(evaluations);
    }

    public async Task<Result<PagedResult<InvestmentCaseDto>>> GetPagedAsync(
        GetInvestmentCasesRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<PagedResult<InvestmentCaseDto>>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "GetInvestmentCasesPaged", authResult.Value);

        var page = await unitOfWork.InvestmentCases.GetPagedAsync(
            request,
            authResult.Value!,
            authorizationService.IsInternalUser,
            cancellationToken);

        var items = page.Items
            .Select(x => caseDtoMapper.MapFromListProjection(x, clock.UtcNow, authorizationService.IsInternalUser))
            .ToList();

        ApplicationLog.Completed(logger,
            "User {UserId} listed investment cases — page {PageNumber}, size {PageSize}, returned {Count} of {Total}",
            authResult.Value,
            page.PageNumber,
            page.PageSize,
            items.Count,
            page.TotalCount);

        return Result<PagedResult<InvestmentCaseDto>>.Ok(
            new PagedResult<InvestmentCaseDto>(items, page.Page, page.PageSize, page.TotalCount));
    }

    public async Task<Result<PresignUploadResponse>> PresignDocumentUploadAsync(Guid caseId, PresignUploadRequest request, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<PresignUploadResponse>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "PresignDocumentUpload", authResult.Value, caseId);

        var entity = await GetCaseWithDocumentsForDocumentWriteAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "PresignDocumentUpload", "case not found", authResult.Value, caseId);
            return Result<PresignUploadResponse>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var validation = ValidatePresignRequest(entity, request);
        if (validation.IsFailure) return Result<PresignUploadResponse>.Fail(validation.Error!);

        var version = entity.Documents.Where(x => x.DocumentType == request.DocumentType).Select(x => x.Version).DefaultIfEmpty(0).Max() + 1;
        var ext = GetSafeExtension(request.FileName);
        var s3Key = $"cases/{entity.CaseNumber}/{request.DocumentType}/{version}{ext}";

        var (url, expiresAt) = await documentStorage.PresignUploadAsync(s3Key, request.MimeType, TimeSpan.FromMinutes(15), cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} received presigned upload URL for {DocumentType} v{Version} on case {CaseId} ({CaseNumber})",
            authResult.Value, request.DocumentType, version, caseId, entity.CaseNumber);

        return Result<PresignUploadResponse>.Ok(new PresignUploadResponse(s3Key, url, expiresAt, version));
    }

    public async Task<Result<CaseDocumentDto>> UploadDocumentAsync(
        Guid caseId,
        PresignUploadRequest request,
        Stream content,
        CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<CaseDocumentDto>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "UploadDocument", authResult.Value, caseId);

        var entity = await GetCaseWithDocumentsForDocumentWriteAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "UploadDocument", "case not found", authResult.Value, caseId);
            return Result<CaseDocumentDto>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var normalized = NormalizePresignRequest(request);
        var keyResult = TryBuildDocumentUploadKey(entity, normalized);
        if (keyResult.IsFailure) return Result<CaseDocumentDto>.Fail(keyResult.Error!);

        var (s3Key, version) = keyResult.Value!;
        if (entity.Documents.Any(x => string.Equals(x.S3Key, s3Key, StringComparison.Ordinal)))
        {
            ApplicationLog.Blocked(logger, "UploadDocument", "document already exists", authResult.Value, caseId);
            return Result<CaseDocumentDto>.Fail(Error.Conflict(ApiMessages.DocumentAlreadyExists));
        }

        var authorization = EnsureCanConfirmDocument(entity, normalized.DocumentType);
        if (authorization.IsFailure) return Result<CaseDocumentDto>.Fail(authorization.Error!);

        await documentStorage.UploadAsync(s3Key, content, normalized.MimeType, cancellationToken);

        var document = await PersistCaseDocumentAsync(
            entity.Id,
            s3Key,
            Path.GetFileName(normalized.FileName),
            normalized.MimeType,
            normalized.FileSize,
            version,
            normalized.DocumentType,
            authResult.Value!,
            cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} uploaded {DocumentType} v{Version} ({FileName}) to case {CaseId} ({CaseNumber})",
            authResult.Value, normalized.DocumentType, version, normalized.FileName, caseId, entity.CaseNumber);

        return await CompleteDocumentConfirmAsync(caseId, document, cancellationToken);
    }

    public async Task<Result<CaseDocumentDto>> ConfirmDocumentUploadedAsync(Guid caseId, string s3Key, string? originalFileName, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<CaseDocumentDto>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "ConfirmDocumentUpload", authResult.Value, caseId);

        var entity = await GetCaseWithDocumentsForDocumentWriteAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "ConfirmDocumentUpload", "case not found", authResult.Value, caseId);
            return Result<CaseDocumentDto>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var keyValidation = ValidateAndNormalizeDocumentKey(entity.CaseNumber, s3Key);
        if (keyValidation.IsFailure) return Result<CaseDocumentDto>.Fail(keyValidation.Error!);

        s3Key = keyValidation.Value!;

        var existingDocument = entity.Documents.FirstOrDefault(x => string.Equals(x.S3Key, s3Key, StringComparison.Ordinal));
        if (existingDocument is not null)
        {
            ApplicationLog.Completed(logger,
                "User {UserId} confirmed existing {DocumentType} v{Version} on case {CaseId} ({CaseNumber})",
                authResult.Value, existingDocument.DocumentType, existingDocument.Version, caseId, entity.CaseNumber);

            return await CompleteDocumentConfirmAsync(caseId, existingDocument, cancellationToken);
        }

        var docType = ExtractDocumentTypeFromKey(s3Key);
        var authorization = EnsureCanConfirmDocument(entity, docType);
        if (authorization.IsFailure) return Result<CaseDocumentDto>.Fail(authorization.Error!);

        var metadata = await documentStorage.GetMetadataAsync(s3Key, cancellationToken);
        if (metadata is null || metadata.ContentLength <= 0)
        {
            ApplicationLog.Blocked(logger, "ConfirmDocumentUpload",
                metadata is null ? "file not found in storage" : "file is empty",
                authResult.Value, caseId);
            return Result<CaseDocumentDto>.Fail(Error.Conflict(
                metadata is null ? ApiMessages.UploadedFileNotFound : ApiMessages.UploadedFileEmpty));
        }

        var ext = Path.GetExtension(s3Key);
        var expectedVersion = entity.Documents.Where(x => x.DocumentType == docType).Select(x => x.Version).DefaultIfEmpty(0).Max() + 1;
        var expectedKey = $"cases/{entity.CaseNumber}/{docType}/{expectedVersion}{ext}";
        if (!string.Equals(s3Key, expectedKey, StringComparison.Ordinal))
        {
            ApplicationLog.Blocked(logger, "ConfirmDocumentUpload", "S3 key does not match expected version", authResult.Value, caseId);
            return Result<CaseDocumentDto>.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var version = expectedVersion;
        var mimeType = string.IsNullOrWhiteSpace(metadata.ContentType)
            ? "application/octet-stream"
            : metadata.ContentType;

        // Use original filename if provided, otherwise fall back to s3Key basename
        var fileName = string.IsNullOrWhiteSpace(originalFileName)
            ? Path.GetFileName(s3Key)
            : originalFileName;

        var document = await PersistCaseDocumentAsync(
            entity.Id,
            s3Key,
            fileName,
            mimeType,
            metadata.ContentLength,
            version,
            docType,
            authResult.Value!,
            cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} confirmed {DocumentType} v{Version} on case {CaseId} ({CaseNumber})",
            authResult.Value, docType, version, caseId, entity.CaseNumber);

        return await CompleteDocumentConfirmAsync(caseId, document, cancellationToken);
    }

    /// <summary>
    /// After the file is in object storage and the document row exists, optionally advance workflow for contract types.
    /// </summary>
    private async Task<Result<CaseDocumentDto>> CompleteDocumentConfirmAsync(
        Guid caseId,
        InvestmentCaseDocument document,
        CancellationToken cancellationToken)
    {
        var workflow = await TryAdvanceWorkflowAfterDocumentConfirmAsync(caseId, document.DocumentType, cancellationToken);
        if (workflow.IsFailure)
            return Result<CaseDocumentDto>.Fail(workflow.Error!);

        return Result<CaseDocumentDto>.Ok(caseDtoMapper.MapDocument(document));
    }

    private async Task<Result> TryAdvanceWorkflowAfterDocumentConfirmAsync(
        Guid caseId,
        DocumentType documentType,
        CancellationToken cancellationToken)
    {
        if (documentType is not (DocumentType.PreContract or DocumentType.SignedContract))
            return Result.Ok();

        if (!authorizationService.HasPermission(CasePermissions.ManageContracts))
            return Result.Ok();

        return documentType switch
        {
            DocumentType.PreContract => await TryAdvancePreliminaryContractWorkflowAsync(caseId, cancellationToken),
            DocumentType.SignedContract => await TryAdvanceSignedContractWorkflowAsync(caseId, cancellationToken),
            _ => Result.Ok()
        };
    }

    private async Task<Result> TryAdvancePreliminaryContractWorkflowAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var status = await dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => x.CurrentStatus)
            .FirstOrDefaultAsync(cancellationToken);

        if (status == CaseStatus.WaitingUserReviewPreliminaryContract)
            return Result.Ok();

        if (status != CaseStatus.WaitingPreliminaryContract)
            return Result.Ok();

        return await ApplyTransitionAsync(
            caseId,
            WorkflowAction.UploadPreliminaryContract,
            "Preliminary contract uploaded",
            cancellationToken);
    }

    private async Task<Result> TryAdvanceSignedContractWorkflowAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var status = await dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => x.CurrentStatus)
            .FirstOrDefaultAsync(cancellationToken);

        if (status == CaseStatus.WaitingFinancialWorksheet)
            return Result.Ok();

        if (status != CaseStatus.WaitingSignedContractUpload)
            return Result.Ok();

        return await ApplyTransitionAsync(
            caseId,
            WorkflowAction.UploadSignedContract,
            "Signed contract uploaded",
            cancellationToken);
    }

    public async Task<Result<PresignDownloadResponse>> PresignDocumentDownloadAsync(Guid caseId, Guid documentId, CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<PresignDownloadResponse>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "PresignDocumentDownload", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.DownloadDocuments))
        {
            ApplicationLog.Blocked(logger, "PresignDocumentDownload", "missing DownloadDocuments permission", authResult.Value, caseId);
            return Result<PresignDownloadResponse>.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var entity = await unitOfWork.InvestmentCases.GetScopedWithDocumentsAsync(caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "PresignDocumentDownload", "case not found", authResult.Value, caseId);
            return Result<PresignDownloadResponse>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var document = entity.Documents.FirstOrDefault(x => x.Id == documentId);
        if (document is null)
        {
            ApplicationLog.Blocked(logger, "PresignDocumentDownload", $"document {documentId} not found", authResult.Value, caseId);
            return Result<PresignDownloadResponse>.Fail(Error.NotFound(ApiMessages.DocumentNotFound));
        }

        var metadata = await documentStorage.GetMetadataAsync(document.S3Key, cancellationToken);
        if (metadata is null || metadata.ContentLength <= 0)
        {
            ApplicationLog.Blocked(logger, "PresignDocumentDownload", "object missing or empty in storage", authResult.Value, caseId);
            return Result<PresignDownloadResponse>.Fail(Error.NotFound(ApiMessages.DocumentNotFound));
        }

        var (url, expiresAt) = await documentStorage.PresignDownloadAsync(document.S3Key, TimeSpan.FromMinutes(10), cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} received presigned download URL for document {DocumentId} ({FileName}) on case {CaseId}",
            authResult.Value, documentId, document.FileName, caseId);

        return Result<PresignDownloadResponse>.Ok(new PresignDownloadResponse(url, expiresAt, document.FileName));
    }

    public async Task<Result<DocumentDownloadFileResult>> DownloadDocumentFileAsync(
        Guid caseId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure) return Result<DocumentDownloadFileResult>.Fail(authResult.Error!);

        ApplicationLog.Started(logger, "DownloadDocumentFile", authResult.Value, caseId);

        if (!authorizationService.HasPermission(CasePermissions.DownloadDocuments))
        {
            ApplicationLog.Blocked(logger, "DownloadDocumentFile", "missing DownloadDocuments permission", authResult.Value, caseId);
            return Result<DocumentDownloadFileResult>.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var entity = await unitOfWork.InvestmentCases.GetScopedWithDocumentsAsync(
            caseId, authResult.Value!, authorizationService.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, "DownloadDocumentFile", "case not found", authResult.Value, caseId);
            return Result<DocumentDownloadFileResult>.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var document = entity.Documents.FirstOrDefault(x => x.Id == documentId);
        if (document is null)
        {
            ApplicationLog.Blocked(logger, "DownloadDocumentFile", $"document {documentId} not found", authResult.Value, caseId);
            return Result<DocumentDownloadFileResult>.Fail(Error.NotFound(ApiMessages.DocumentNotFound));
        }

        var metadata = await documentStorage.GetMetadataAsync(document.S3Key, cancellationToken);
        if (metadata is null || metadata.ContentLength <= 0)
            return Result<DocumentDownloadFileResult>.Fail(Error.NotFound(ApiMessages.DocumentNotFound));

        var stream = await documentStorage.OpenReadAsync(document.S3Key, cancellationToken);
        var contentType = string.IsNullOrWhiteSpace(document.MimeType)
            ? metadata.ContentType ?? "application/octet-stream"
            : document.MimeType;

        ApplicationLog.Completed(logger,
            "User {UserId} downloaded document {DocumentId} ({FileName}) from case {CaseId}",
            authResult.Value, documentId, document.FileName, caseId);

        return Result<DocumentDownloadFileResult>.Ok(
            new DocumentDownloadFileResult(stream, contentType, document.FileName));
    }

    private Result<string> RequireUserId()
    {
        var auth = authorizationService.EnsureAuthenticated();
        if (auth.IsFailure) return Result<string>.Fail(auth.Error!);
        return Result<string>.Ok(authorizationService.UserId!);
    }

    private sealed record CaseMeta(string CaseNumber, CaseStatus Status);

    private sealed record PaymentCaseContext(string CaseNumber, CaseStatus Status);

    private Task<PaymentCaseContext?> GetPaymentCaseContextAsync(Guid caseId, CancellationToken cancellationToken)
        => dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new PaymentCaseContext(x.CaseNumber, x.CurrentStatus))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<Result> TryAutoCompleteCaseAfterPaymentsAsync(
        Guid caseId,
        string userId,
        CancellationToken cancellationToken)
    {
        var totals = await dbContext.InvestmentCases
            .AsNoTracking()
            .Where(c => c.Id == caseId && c.CurrentStatus == CaseStatus.WaitingPayment)
            .Select(c => new
            {
                Approved = c.FinancialWorksheet != null ? c.FinancialWorksheet.ApprovedAmount : 0m,
                TotalConfirmed = c.Payments
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .Sum(p => (decimal?)p.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (totals is null || totals.Approved <= 0 || totals.TotalConfirmed < totals.Approved)
            return Result.Ok();

        return await ApplyTransitionAsync(
            caseId,
            WorkflowAction.CompletePayment,
            "Automatic completion based on full payment confirmation.",
            cancellationToken);
    }

    private void UntrackCaseRoot(InvestmentCase entity)
    {
        if (dbContext is not DbContext efContext)
            return;

        var entry = efContext.Entry(entity);
        if (entry.State is EntityState.Modified or EntityState.Added)
            entry.State = EntityState.Unchanged;
    }

    /// <summary>
    /// Persists status/phase via ExecuteUpdate so legacy xmin/RowVersion on investment_cases cannot cause concurrency failures.
    /// </summary>
    private async Task<Result> PersistCaseTransitionAsync(
        InvestmentCase entity,
        int commentsCountBefore,
        CancellationToken cancellationToken)
    {
        var history = entity.WorkflowHistory[^1];
        var pendingComments = entity.Comments.Skip(commentsCountBefore).ToList();

        if (dbContext is DbContext efContext)
            efContext.ChangeTracker.Clear();

        var rows = await dbContext.InvestmentCases.ApplyStateAsync(
            entity.Id,
            entity.CurrentStatus,
            entity.CurrentPhase,
            entity.UpdatedAt ?? clock.UtcNow,
            entity.CompletedAt,
            cancellationToken);

        if (rows == 0)
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));

        await dbContext.CaseWorkflowHistories.AddAsync(history, cancellationToken);
        foreach (var pendingComment in pendingComments)
            await dbContext.CaseComments.AddAsync(pendingComment, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static bool SupportsInternalApproveComment(WorkflowAction action, CaseStatus statusBefore) =>
        action switch
        {
            WorkflowAction.Approve => statusBefore is CaseStatus.ReviewDataEntry1 or CaseStatus.ReviewDataEntry2,
            WorkflowAction.ApproveFinancialWorksheet => statusBefore == CaseStatus.FinancialWorksheetReview,
            _ => false
        };

    private static bool IsFreeformCommentAllowed(CaseStatus status, CasePhase phase) =>
        status == CaseStatus.WaitingUserReviewPreliminaryContract && phase == CasePhase.Legal;

    private async Task<CaseMeta?> GetCaseMetaForChildWriteAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        var query = dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => x.Id == caseId);

        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return await query
            .Select(x => new CaseMeta(x.CaseNumber, x.CurrentStatus))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Read-only load for document presign/confirm. Avoids tracking <see cref="InvestmentCase"/> so saving a new
    /// <see cref="InvestmentCaseDocument"/> does not UPDATE the parent row (legacy xmin concurrency on investment_cases).
    /// </summary>
    private Task<InvestmentCase?> GetCaseWithDocumentsForDocumentWriteAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        var query = dbContext.InvestmentCases
            .AsNoTracking()
            .Include(x => x.Documents)
            .Where(x => x.Id == caseId);

        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<InvestmentCaseDocument> PersistCaseDocumentAsync(
        Guid caseId,
        string s3Key,
        string fileName,
        string mimeType,
        long fileSize,
        int version,
        DocumentType documentType,
        string uploadedByUserId,
        CancellationToken cancellationToken)
    {
        var uploadedAt = clock.UtcNow;
        var document = new InvestmentCaseDocument(
            caseId,
            s3Key,
            fileName,
            mimeType,
            fileSize,
            version,
            documentType,
            uploadedByUserId,
            uploadedAt);

        await dbContext.CaseDocuments.AddAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return document;
    }

    private static PresignUploadRequest NormalizePresignRequest(PresignUploadRequest request)
    {
        var mimeType = NormalizeMimeType(request.FileName, request.MimeType);
        return request with { MimeType = mimeType };
    }

    private Result<(string S3Key, int Version)> TryBuildDocumentUploadKey(InvestmentCase entity, PresignUploadRequest request)
    {
        var validation = ValidatePresignRequest(entity, request);
        if (validation.IsFailure) return Result<(string, int)>.Fail(validation.Error!);

        var version = entity.Documents.Where(x => x.DocumentType == request.DocumentType).Select(x => x.Version).DefaultIfEmpty(0).Max() + 1;
        var ext = GetSafeExtension(request.FileName);
        var s3Key = $"cases/{entity.CaseNumber}/{request.DocumentType}/{version}{ext}";
        return Result<(string, int)>.Ok((s3Key, version));
    }

    private Result ValidatePresignRequest(InvestmentCase entity, PresignUploadRequest request)
    {
        if (!IsSafeClientFileName(request.FileName))
            return Result.Fail(Error.Validation(ApiMessages.InvalidFileName));

        var ext = GetSafeExtension(request.FileName);
        if (!IsAllowedUpload(request.MimeType, ext))
            return Result.Fail(Error.Validation(ApiMessages.FileTypeNotAllowed));

        if (authorizationService.IsInternalUser)
        {
            if (request.DocumentType is DocumentType.PreContract or DocumentType.FinalContract or DocumentType.SignedContract)
            {
                if (!authorizationService.HasPermission(CasePermissions.ManageContracts))
                    return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

                if (request.DocumentType is DocumentType.PreContract && entity.CurrentStatus != CaseStatus.WaitingPreliminaryContract)
                    return Result.Fail(Error.Conflict(ApiMessages.DocumentUploadNotAllowed));

                if (request.DocumentType is DocumentType.FinalContract && entity.CurrentStatus is not (CaseStatus.ContractDrafting or CaseStatus.WaitingContractSignature))
                    return Result.Fail(Error.Conflict(ApiMessages.DocumentUploadNotAllowed));

                if (request.DocumentType is DocumentType.SignedContract && entity.CurrentStatus != CaseStatus.WaitingSignedContractUpload)
                    return Result.Fail(Error.Conflict(ApiMessages.DocumentUploadNotAllowed));

                return Result.Ok();
            }

            if (request.DocumentType is DocumentType.PaymentReceipt)
            {
                if (!authorizationService.HasPermission(CasePermissions.ManagePayments))
                    return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

                if (entity.CurrentStatus != CaseStatus.WaitingPayment)
                    return Result.Fail(Error.Conflict(ApiMessages.DocumentUploadNotAllowed));

                return Result.Ok();
            }

            if (!authorizationService.HasPermission(CasePermissions.UploadDocuments))
                return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

            return Result.Ok();
        }

        if (!authorizationService.HasPermission(CasePermissions.UploadDocuments))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        if (request.DocumentType is DocumentType.PreContract or DocumentType.FinalContract or DocumentType.SignedContract or DocumentType.PaymentReceipt)
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        if (entity.CurrentStatus is not (CaseStatus.Draft or CaseStatus.DataEntry1 or CaseStatus.DataEntry2))
            return Result.Fail(Error.Conflict(ApiMessages.DocumentUploadNotAllowed));

        return Result.Ok();
    }

    private Result EnsureCanConfirmDocument(InvestmentCase entity, DocumentType documentType)
    {
        if (!authorizationService.IsInternalUser)
        {
            if (documentType is DocumentType.PreContract or DocumentType.FinalContract or DocumentType.SignedContract or DocumentType.PaymentReceipt)
                return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

            // Allow confirm while review is pending so uploads started during data entry can finish after submit.
            if (entity.CurrentStatus is not (
                CaseStatus.Draft
                or CaseStatus.DataEntry1
                or CaseStatus.DataEntry2
                or CaseStatus.ReviewDataEntry1
                or CaseStatus.ReviewDataEntry2))
                return Result.Fail(Error.Conflict(ApiMessages.DocumentConfirmationNotAllowed));

            return Result.Ok();
        }

        if (documentType is DocumentType.PreContract or DocumentType.FinalContract or DocumentType.SignedContract)
        {
            if (!authorizationService.HasPermission(CasePermissions.ManageContracts))
                return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

            if (documentType is DocumentType.PreContract && entity.CurrentStatus != CaseStatus.WaitingPreliminaryContract)
                return Result.Fail(Error.Conflict(ApiMessages.DocumentConfirmationNotAllowed));

            if (documentType is DocumentType.FinalContract && entity.CurrentStatus is not (CaseStatus.ContractDrafting or CaseStatus.WaitingContractSignature))
                return Result.Fail(Error.Conflict(ApiMessages.DocumentConfirmationNotAllowed));

            if (documentType is DocumentType.SignedContract && entity.CurrentStatus != CaseStatus.WaitingSignedContractUpload)
                return Result.Fail(Error.Conflict(ApiMessages.DocumentConfirmationNotAllowed));

            return Result.Ok();
        }

        if (documentType is DocumentType.PaymentReceipt)
        {
            if (!authorizationService.HasPermission(CasePermissions.ManagePayments))
                return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

            if (entity.CurrentStatus != CaseStatus.WaitingPayment)
                return Result.Fail(Error.Conflict(ApiMessages.DocumentConfirmationNotAllowed));

            return Result.Ok();
        }

        return authorizationService.HasPermission(CasePermissions.UploadDocuments) ? Result.Ok() : Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
    }

    private static Result<string> ValidateAndNormalizeDocumentKey(string caseNumber, string s3Key)
    {
        if (string.IsNullOrWhiteSpace(s3Key))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        if (s3Key.Contains('\\') || s3Key.Contains("..", StringComparison.Ordinal))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        if (!s3Key.StartsWith($"cases/{caseNumber}/", StringComparison.Ordinal))
            return Result<string>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var parts = s3Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        if (!Enum.TryParse<DocumentType>(parts[2], ignoreCase: true, out _))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        var file = parts[3];
        if (file.Length > 128)
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        var ext = Path.GetExtension(file);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10)
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
        if (!int.TryParse(fileNameWithoutExt, out _))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        return Result<string>.Ok(s3Key);
    }

    private static DocumentType ExtractDocumentTypeFromKey(string s3Key)
    {
        var parts = s3Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return Enum.TryParse<DocumentType>(parts[2], ignoreCase: true, out var docType) ? docType : DocumentType.Other;
    }

    private static bool IsSafeClientFileName(string fileName)
        => !string.IsNullOrWhiteSpace(fileName) && string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal);

    private static string GetSafeExtension(string fileName)
    {
        var ext = Path.GetExtension(Path.GetFileName(fileName));
        return string.IsNullOrWhiteSpace(ext) ? string.Empty : ext.ToLowerInvariant();
    }

    private static string NormalizeMimeType(string fileName, string mimeType)
    {
        return GetSafeExtension(fileName) switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType.Trim()
        };
    }

    private static bool IsAllowedUpload(string mimeType, string ext)
    {
        if (string.IsNullOrWhiteSpace(mimeType) || string.IsNullOrWhiteSpace(ext))
            return false;

        return (mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) && ext == ".pdf")
               || (mimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) && (ext == ".jpg" || ext == ".jpeg"))
               || (mimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase) && ext == ".png");
    }

    private static Result ValidateCommentAttachmentKey(string caseNumber, Guid commentId, string s3Key, string fileName)
    {
        if (string.IsNullOrWhiteSpace(s3Key))
            return Result.Fail(Error.Validation(ApiMessages.InvalidAttachmentKey));

        if (!IsSafeClientFileName(fileName))
            return Result.Fail(Error.Validation(ApiMessages.InvalidFileName));

        if (s3Key.Contains('\\') || s3Key.Contains("..", StringComparison.Ordinal))
            return Result.Fail(Error.Validation(ApiMessages.InvalidAttachmentKey));

        var prefix = $"cases/{caseNumber}/comments/{commentId:D}/";
        if (!s3Key.StartsWith(prefix, StringComparison.Ordinal))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        return Result.Ok();
    }

    private static IEnumerable<InvestmentCaseDocument> SelectLatestDocuments(IEnumerable<InvestmentCaseDocument> documents)
        => documents
            .GroupBy(d => d.DocumentType)
            .Select(g => g.OrderByDescending(d => d.Version).ThenByDescending(d => d.UploadedAt).First());

    private CaseDocumentTypeVersionsDto BuildDocumentTypeVersions(
        IEnumerable<InvestmentCaseDocument> documents,
        DocumentType documentType,
        Func<InvestmentCaseDocument, CaseDocumentDto> map)
    {
        var versions = documents
            .Where(d => d.DocumentType == documentType)
            .OrderByDescending(d => d.Version)
            .ThenByDescending(d => d.UploadedAt)
            .Select(map)
            .ToList();

        return caseDtoMapper.MapDocumentTypeVersions(documentType, versions);
    }

    private async Task<Result<(string FullName, string Email)>> ResolveApplicantIdentityAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var id))
            return Result<(string, string)>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
            return Result<(string, string)>.Fail(Error.NotFound(ApiMessages.AuthenticationRequired));

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        var email = user.Email?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            return Result<(string, string)>.Fail(Error.Validation(ApiMessages.ApplicantProfileIncomplete));

        return Result<(string FullName, string Email)>.Ok((fullName, email));
    }

    private async Task<ApplicantContactDto?> LoadApplicantContactAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var id))
            return null;

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return user is null ? null : caseDtoMapper.MapApplicantContact(user);
    }
}
