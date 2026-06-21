using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Observability.Correlation;
using Core.Application.Abstractions;
using Core.Application.Authorization;
using Core.Application.Common;
using Core.Application.DTOs;
using Core.Application.Logging;
using Core.Application.Mappers;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Domain.Constants;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Core.Domain.Identity;
using Core.Domain.Identity.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public sealed class LoanCaseAppService(
    ICoreUnitOfWork unitOfWork,
    ICoreDbContext dbContext,
    ILoanCaseStateManager stateManager,
    ILoanWorkflowOrchestrator workflowOrchestrator,
    ILoanCaseNumberGenerator caseNumberGenerator,
    IDocumentStorage documentStorage,
    BuildingBlocks.Domain.Abstractions.IClock clock,
    IUserContext userContext,
    ILoanAuthorizationService authorizationService,
    ILoanCaseDtoMapper dtoMapper,
    IUserDisplayLookup userDisplayLookup,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LoanCaseAppService> logger) : ILoanCaseAppService
{
    public async Task<Result<LoanCaseDto>> CreateAsync(CreateLoanCaseRequest request, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<LoanCaseDto>.Fail(auth.Error!);

        if (!authorizationService.HasPermission(LoanPermissions.Create))
            return Result<LoanCaseDto>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var caseNumber = await caseNumberGenerator.GenerateLoanCaseAsync(ct);
        var entity = new LoanCase(caseNumber, auth.Value!, request.ApplicantType);
        Company? linkedCompany = null;

        if (request.ApplicantType == ApplicantType.Company)
        {
            if (request.CompanyId is null || request.CompanyId == Guid.Empty)
                return Result<LoanCaseDto>.Fail(Error.Validation(ApiMessages.CompanyRequiredForCompanyApplicant));

            if (!Guid.TryParse(auth.Value, out var userId))
                return Result<LoanCaseDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

            linkedCompany = await unitOfWork.Companies.FirstOrDefaultAsync(
                c => c.Id == request.CompanyId.Value,
                asNoTracking: true,
                cancellationToken: ct);

            if (linkedCompany is null)
                return Result<LoanCaseDto>.Fail(Error.NotFound(ApiMessages.CompanyNotFound));

            if (linkedCompany.OwnerUserId != userId)
                return Result<LoanCaseDto>.Fail(Error.Forbidden(ApiMessages.CompanyAccessDenied));

            entity.AssignCompany(linkedCompany.Id);
        }

        entity.SetTitle(request.Title);

        var workflowInstanceId = await workflowOrchestrator.StartLoanCaseAsync(entity.Id, ct);
        entity.AttachWorkflowInstance(workflowInstanceId);

        await unitOfWork.LoanCases.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<LoanCaseDto>.Ok(dtoMapper.MapCase(entity, authorizationService.IsInternalUser, linkedCompany));
    }

    public async Task<Result<LoanCaseDto>> GetAsync(Guid caseId, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<LoanCaseDto>.Fail(auth.Error!);

        var detail = await unitOfWork.LoanCases.GetDetailProjectionAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (detail is null)
            return Result<LoanCaseDto>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        var includeRepaymentData = detail.CurrentStatus is LoanCaseStatus.RepaymentPhase or LoanCaseStatus.Completed;
        var fundCreditCapacity = await ResolveFundCreditCapacityForCaseAsync(detail.CurrentStatus, ct);

        IReadOnlyList<LoanInstallmentListProjection>? installments = null;
        IReadOnlyList<LoanPaymentListProjection>? payments = null;
        IReadOnlyDictionary<string, UserDisplayDto>? userLookup = null;

        if (includeRepaymentData)
        {
            installments = await unitOfWork.LoanCases.GetInstallmentProjectionsAsync(
                caseId, auth.Value!, authorizationService.IsInternalUser, ct);
            payments = await unitOfWork.LoanCases.GetPaymentProjectionsAsync(
                caseId, auth.Value!, authorizationService.IsInternalUser, ct);
            userLookup = await userDisplayLookup.GetByIdsAsync(payments.Select(x => x.CreatedByUserId), ct);
        }

        if (authorizationService.IsInternalUser
            && (string.IsNullOrWhiteSpace(detail.ApplicantFullName) || string.IsNullOrWhiteSpace(detail.ApplicantPhoneNumber)))
        {
            var applicantDisplay = await ResolveApplicantDisplayAsync(detail.ApplicantUserId, ct);
            detail = detail with
            {
                ApplicantFullName = detail.ApplicantFullName ?? applicantDisplay?.FullName,
                ApplicantPhoneNumber = detail.ApplicantPhoneNumber ?? applicantDisplay?.PhoneNumber
            };
        }

        return Result<LoanCaseDto>.Ok(
            dtoMapper.MapFromDetailProjection(
                detail,
                authorizationService.IsInternalUser,
                fundCreditCapacity,
                installments,
                payments,
                userLookup));
    }

    public async Task<Result<LoanCaseDto>> UpdateTitleAsync(Guid caseId, UpdateCaseTitleRequest request, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<LoanCaseDto>.Fail(auth.Error!);

        var isInternal = authorizationService.IsInternalUser;
        var exists = await dbContext.LoanCases
            .AsNoTracking()
            .AnyAsync(x => x.Id == caseId && (isInternal || x.ApplicantUserId == auth.Value), ct);

        if (!exists)
            return Result<LoanCaseDto>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        var title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        var rows = await dbContext.LoanCases.SetTitleAsync(caseId, title, clock.UtcNow, ct);

        if (rows == 0)
            return Result<LoanCaseDto>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        return await GetAsync(caseId, ct);
    }

    public async Task<Result<PagedResult<LoanCaseDto>>> GetPagedAsync(GetLoanCasesRequest request, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<PagedResult<LoanCaseDto>>.Fail(auth.Error!);

        var page = await unitOfWork.LoanCases.GetPagedAsync(
            request,
            auth.Value!,
            authorizationService.IsInternalUser,
            ct);

        var items = page.Items
            .Select(x => dtoMapper.MapFromListProjection(x, authorizationService.IsInternalUser))
            .ToList();

        return Result<PagedResult<LoanCaseDto>>.Ok(
            new PagedResult<LoanCaseDto>(items, page.Skip, page.Take, page.TotalCount));
    }

    public async Task<Result<IEnumerable<LoanWorkflowHistoryDto>>> GetHistoryAsync(Guid caseId, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<IEnumerable<LoanWorkflowHistoryDto>>.Fail(auth.Error!);

        var history = await unitOfWork.LoanCases.GetWorkflowHistoryAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (history.Count == 0)
        {
            var exists = await dbContext.LoanCases
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == caseId
                         && (authorizationService.IsInternalUser || x.ApplicantUserId == auth.Value),
                    ct);

            if (!exists)
                return Result<IEnumerable<LoanWorkflowHistoryDto>>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));
        }

        return Result<IEnumerable<LoanWorkflowHistoryDto>>.Ok(history.Select(dtoMapper.MapHistory));
    }

    public async Task<Result> UpdateApplicationAsync(Guid caseId, UpdateLoanApplicationRequest request, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        var isInternal = authorizationService.IsInternalUser;
        var currentStatus = await dbContext.LoanCases
            .AsNoTracking()
            .Where(x => x.Id == caseId && (isInternal || x.ApplicantUserId == auth.Value))
            .Select(x => (LoanCaseStatus?)x.CurrentStatus)
            .FirstOrDefaultAsync(ct);

        if (currentStatus is null)
            return Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        if (currentStatus is not (LoanCaseStatus.Draft or LoanCaseStatus.DataEntry or LoanCaseStatus.RevisionRequestedByCredit))
            return Result.Fail(Error.Conflict(ApiMessages.LoanApplicationNotEditable));

        if (!LoanApplicationCompleteness.HasMinimumData(
                request.RequestedAmount,
                request.FacilitySubject,
                request.OfferedGuarantees))
        {
            return Result.Fail(Error.Validation(ApiMessages.LoanApplicationIncomplete));
        }

        await LoanCaseApplicationPersistence.UpsertAsync(dbContext, caseId, request, ct);
        await dbContext.LoanCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<Result> BeginDataEntryAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Submit, null, ct);

    public Task<Result> SubmitApplicationAsync(Guid caseId, string? comment, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Submit, comment, ct);

    public async Task<Result> UpdateApprovalDetailAsync(Guid caseId, UpdateLoanApprovalDetailRequest request, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        if (!authorizationService.HasPermission(LoanPermissions.ManageApprovalDetail))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await unitOfWork.LoanCases.GetScopedForTransitionAsync(
            caseId, auth.Value!, isInternalUser: true, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        if (entity.CurrentStatus is not (LoanCaseStatus.PendingCreditReview or LoanCaseStatus.RevisionRequestedByCredit))
            return Result.Fail(Error.Conflict(ApiMessages.LoanApprovalDetailNotEditable));

        var detail = await dbContext.LoanApprovalDetails.FirstOrDefaultAsync(x => x.CaseId == caseId, ct);
        if (detail is null)
        {
            detail = new LoanApprovalDetail(caseId);
            await dbContext.LoanApprovalDetails.AddAsync(detail, ct);
        }

        detail.Update(
            request.DebtToAssetRatio,
            request.CurrentRatio,
            request.ProfitabilityRatioPercent,
            request.CreditLimitWithCheck,
            request.IsCreditLineActive,
            request.RemainingCreditAfterGrant,
            request.FacilityType,
            request.ContractSubject,
            request.BrokerageAndRelatedContract,
            request.ApprovedAmount,
            request.ApprovedAmountInWords,
            request.RepaymentMonths,
            request.GracePeriodMonths,
            request.AnnualProfitRatePercent,
            request.DailyPenaltyRatePercent,
            request.CollateralDescription,
            request.GuarantorsDescription,
            request.OtherNotes,
            request.ExpectedTotalProfit,
            request.RepaymentCheckAmount);

        await dbContext.LoanCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<Result> ApproveCreditReviewAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Approve, comment, ct, internalComment);

    public Task<Result> RequestCreditRevisionAsync(Guid caseId, string message, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.RequestRevision, message, ct);

    public Task<Result> CeoApproveInitialAsync(Guid caseId, string? comment, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Approve, comment, ct);

    public Task<Result> CeoRejectInitialAsync(Guid caseId, string reason, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Reject, reason, ct);

    public async Task<Result> UpsertInstallmentsAsync(Guid caseId, UpsertLoanInstallmentsRequest request, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        if (!authorizationService.HasPermission(LoanPermissions.ManageInstallments))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await unitOfWork.LoanCases.GetScopedForTransitionAsync(
            caseId, auth.Value!, isInternalUser: true, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        if (entity.CurrentStatus != LoanCaseStatus.PendingLegalRawContract)
            return Result.Fail(Error.Conflict(ApiMessages.LoanInstallmentsNotEditable));

        var existing = await dbContext.LoanInstallments.Where(x => x.CaseId == caseId).ToListAsync(ct);
        dbContext.LoanInstallments.RemoveRange(existing);

        var newInstallments = request.Installments
            .OrderBy(x => x.RowNumber)
            .Select(x => new LoanInstallment(
                caseId,
                x.RowNumber,
                x.InstallmentDate,
                x.PrincipalAmount,
                x.ProfitAmount,
                x.TotalAmount,
                x.FundShareOfPrincipal,
                x.FundShareOfProfit,
                x.FundShareOfTotal,
                x.IsGracePeriod))
            .ToList();

        await dbContext.LoanInstallments.AddRangeAsync(newInstallments, ct);
        await dbContext.LoanCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<Result> CompleteLegalSetupAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.SubmitInstallments, null, ct);

    public Task<Result> SubmitSignedPackageAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.SubmitSignedPackage, null, ct);

    public Task<Result> ApproveLegalReviewAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Approve, comment, ct, internalComment);

    public Task<Result> RequestLegalRevisionAsync(Guid caseId, string message, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.RequestRevision, message, ct);

    public Task<Result> ApproveFinancialReviewAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Approve, comment, ct, internalComment);

    public Task<Result> RequestFinancialRevisionAsync(Guid caseId, string message, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.RequestRevision, message, ct);

    public Task<Result> ConfirmFinalContractUploadedAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.UploadFinalContract, null, ct);

    public Task<Result> CeoApproveFinalAsync(Guid caseId, string? comment, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Approve, comment, ct);

    public Task<Result> CeoRejectFinalAsync(Guid caseId, string reason, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Reject, reason, ct);

    public async Task<Result> RegisterPaymentAsync(Guid caseId, RegisterLoanPaymentRequest request, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        if (!authorizationService.HasPermission(LoanPermissions.ManagePayments))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await unitOfWork.LoanCases.GetScopedForTransitionAsync(
            caseId, auth.Value!, isInternalUser: true, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        if (entity.CurrentStatus is not LoanCaseStatus.ReadyForPayment)
            return Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));

        var payment = entity.AddPayment(
            request.Amount,
            request.PaymentDate,
            request.TransactionNumber,
            request.ReceiptS3Key,
            request.Notes,
            request.StageNumber,
            auth.Value!);

        await dbContext.LoanPayments.AddAsync(payment, ct);
        await dbContext.LoanCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);

        if (entity.CurrentStatus == LoanCaseStatus.ReadyForPayment)
            return await ApplyTransitionAsync(caseId, LoanWorkflowAction.RegisterPayment, null, ct);

        return Result.Ok();
    }

    public async Task<Result<IEnumerable<LoanPaymentDto>>> ListPaymentsAsync(Guid caseId, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<IEnumerable<LoanPaymentDto>>.Fail(auth.Error!);

        var entity = await unitOfWork.LoanCases.GetScopedAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<IEnumerable<LoanPaymentDto>>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        var userLookup = await userDisplayLookup.GetByIdsAsync(entity.Payments.Select(x => x.CreatedByUserId), ct);
        return Result<IEnumerable<LoanPaymentDto>>.Ok(
            entity.Payments
                .OrderBy(x => x.StageNumber)
                .Select(p => dtoMapper.MapPayment(
                    p,
                    userDisplayLookup.ResolveFullName(userLookup, p.CreatedByUserId))));
    }

    public async Task<Result<IEnumerable<LoanInstallmentDto>>> ListInstallmentsAsync(Guid caseId, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<IEnumerable<LoanInstallmentDto>>.Fail(auth.Error!);

        var entity = await unitOfWork.LoanCases.GetScopedAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<IEnumerable<LoanInstallmentDto>>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        return Result<IEnumerable<LoanInstallmentDto>>.Ok(
            entity.Installments.OrderBy(x => x.RowNumber).Select(dtoMapper.MapInstallment));
    }

    public async Task<Result> MarkInstallmentPaidAsync(
        Guid caseId,
        Guid installmentId,
        MarkLoanInstallmentPaidRequest request,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        var actorRole = ResolveActorRole();
        var hasManagePayments = authorizationService.HasPermission(LoanPermissions.ManagePayments);
        var hasRepayInstallments = authorizationService.HasPermission(LoanPermissions.RepayInstallments);

        if (!hasManagePayments && !hasRepayInstallments)
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await unitOfWork.LoanCases.GetScopedAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        if (entity.CurrentStatus is not LoanCaseStatus.RepaymentPhase)
            return Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));

        if (hasRepayInstallments && !hasManagePayments
            && !string.Equals(UserRoleClaims.Normalize(actorRole), UserRoleClaims.Applicant, StringComparison.OrdinalIgnoreCase))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var installment = entity.Installments.FirstOrDefault(x => x.Id == installmentId);
        if (installment is null)
            return Result.Fail(Error.NotFound("قسط یافت نشد."));

        if (installment.IsGracePeriod)
            return Result.Fail(Error.Conflict("قسط دوره تنفس نیازی به پرداخت ندارد."));

        if (installment.IsPaid)
            return Result.Fail(Error.Conflict("این قساط قبلاً پرداخت شده است."));

        if (Math.Round(request.Amount, 2) != Math.Round(installment.TotalAmount, 2))
            return Result.Fail(Error.Validation("مبلغ پرداخت باید با مبلغ قساط برابر باشد."));

        if (string.IsNullOrWhiteSpace(request.ReceiptS3Key))
            return Result.Fail(Error.Validation("بارگذاری رسید پرداخت الزامی است."));

        var transactionNumber = (request.TransactionNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(transactionNumber))
            return Result.Fail(Error.Validation("شماره تراکنش الزامی است."));

        var paidAt = request.PaidDate.HasValue
            ? new DateTimeOffset(request.PaidDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : clock.UtcNow;

        var repaymentStageNumber = LoanPaymentStageNumbers.ForInstallmentRepayment(installment.RowNumber);
        if (entity.Payments.Any(x => x.StageNumber == repaymentStageNumber))
            return Result.Fail(Error.Conflict("پرداخت این قساط قبلاً ثبت شده است."));

        var payment = entity.AddPayment(
            request.Amount,
            request.PaidDate ?? DateOnly.FromDateTime(paidAt.UtcDateTime),
            transactionNumber,
            request.ReceiptS3Key,
            request.Notes,
            repaymentStageNumber,
            auth.Value!);

        installment.MarkPaid(paidAt);
        await dbContext.LoanPayments.AddAsync(payment, ct);
        await dbContext.LoanCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result.Fail(Error.Conflict("ثبت پرداخت قساط انجام نشد. لطفاً دوباره تلاش کنید."));
        }

        return Result.Ok();
    }

    public Task<Result> CompleteRepaymentAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, LoanWorkflowAction.Approve, null, ct);

    public async Task<Result<PresignLoanUploadResponse>> PresignDocumentUploadAsync(
        Guid caseId,
        PresignLoanUploadRequest request,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<PresignLoanUploadResponse>.Fail(auth.Error!);

        if (!authorizationService.HasPermission(LoanPermissions.UploadDocuments))
            return Result<PresignLoanUploadResponse>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await unitOfWork.LoanCases.GetScopedWithDocumentsAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<PresignLoanUploadResponse>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        var version = entity.Documents
            .Where(x => !x.IsDeleted && x.DocumentType == request.DocumentType)
            .Select(x => x.Version)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var ext = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
        var s3Key = $"loan-cases/{entity.CaseNumber}/{(int)request.DocumentType}/{version}{ext}";

        var (url, expiresAt) = await documentStorage.PresignUploadAsync(s3Key, request.MimeType, TimeSpan.FromMinutes(15), ct);
        return Result<PresignLoanUploadResponse>.Ok(new PresignLoanUploadResponse(s3Key, url, expiresAt, version));
    }

    public async Task<Result<LoanCaseDocumentDto>> ConfirmDocumentUploadedAsync(Guid caseId, string s3Key, string? originalFileName, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<LoanCaseDocumentDto>.Fail(auth.Error!);

        var entity = await unitOfWork.LoanCases.GetScopedWithDocumentsAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<LoanCaseDocumentDto>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        var keyValidation = ValidateAndNormalizeLoanDocumentKey(entity.CaseNumber, s3Key);
        if (keyValidation.IsFailure)
            return Result<LoanCaseDocumentDto>.Fail(keyValidation.Error!);

        s3Key = keyValidation.Value!;

        var existing = entity.Documents.FirstOrDefault(x => string.Equals(x.S3Key, s3Key, StringComparison.Ordinal));
        if (existing is not null)
            return Result<LoanCaseDocumentDto>.Ok(dtoMapper.MapDocument(existing));

        var metadata = await documentStorage.GetMetadataAsync(s3Key, ct);
        if (metadata is null || metadata.ContentLength <= 0)
            return Result<LoanCaseDocumentDto>.Fail(Error.Conflict(ApiMessages.UploadedFileNotFound));

        var docType = ExtractLoanDocumentTypeFromKey(s3Key);
        var version = ExtractLoanDocumentVersionFromKey(s3Key);
        var mimeType = string.IsNullOrWhiteSpace(metadata.ContentType) ? "application/octet-stream" : metadata.ContentType;
        
        // Use original filename if provided, otherwise fall back to s3Key basename
        var fileName = string.IsNullOrWhiteSpace(originalFileName)
            ? Path.GetFileName(s3Key)
            : originalFileName;

        var document = entity.AddDocument(
            s3Key,
            fileName,
            mimeType,
            metadata.ContentLength,
            version,
            docType,
            auth.Value!);

        await dbContext.LoanCaseDocuments.AddAsync(document, ct);
        await dbContext.LoanCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await TryAutoAdvanceAfterDocumentAsync(caseId, docType, ct);
        return Result<LoanCaseDocumentDto>.Ok(dtoMapper.MapDocument(document));
    }

    public async Task<Result<IEnumerable<LoanCaseDocumentDto>>> ListDocumentsAsync(Guid caseId, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<IEnumerable<LoanCaseDocumentDto>>.Fail(auth.Error!);

        var entity = await unitOfWork.LoanCases.GetScopedAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<IEnumerable<LoanCaseDocumentDto>>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        var documents = entity.Documents.Where(x => !x.IsDeleted).ToList();
        var userLookup = await userDisplayLookup.GetByIdsAsync(documents.Select(x => x.UploadedByUserId), ct);
        return Result<IEnumerable<LoanCaseDocumentDto>>.Ok(
            documents.Select(d => dtoMapper.MapDocument(
                d,
                userDisplayLookup.ResolveFullName(userLookup, d.UploadedByUserId))));
    }

    public async Task<Result<DocumentDownloadFileResult>> DownloadDocumentFileAsync(
        Guid caseId,
        Guid documentId,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result<DocumentDownloadFileResult>.Fail(auth.Error!);

        if (!authorizationService.HasPermission(LoanPermissions.DownloadDocuments))
            return Result<DocumentDownloadFileResult>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await unitOfWork.LoanCases.GetScopedWithDocumentsAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<DocumentDownloadFileResult>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        var document = entity.Documents.FirstOrDefault(x => x.Id == documentId && !x.IsDeleted);
        if (document is null)
            return Result<DocumentDownloadFileResult>.Fail(Error.NotFound(ApiMessages.DocumentNotFound));

        var metadata = await documentStorage.GetMetadataAsync(document.S3Key, ct);
        if (metadata is null || metadata.ContentLength <= 0)
            return Result<DocumentDownloadFileResult>.Fail(Error.NotFound(ApiMessages.DocumentNotFound));

        var stream = await documentStorage.OpenReadAsync(document.S3Key, ct);
        var contentType = string.IsNullOrWhiteSpace(document.MimeType)
            ? metadata.ContentType ?? "application/octet-stream"
            : document.MimeType;

        return Result<DocumentDownloadFileResult>.Ok(
            new DocumentDownloadFileResult(stream, contentType, document.FileName));
    }

    public async Task<Result<IEnumerable<LoanCaseCommentDto>>> ListCommentsAsync(
        Guid caseId,
        bool includeInternal,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<IEnumerable<LoanCaseCommentDto>>.Fail(auth.Error!);

        var comments = await unitOfWork.LoanCases.GetCommentsAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (comments.Count == 0)
        {
            var exists = await dbContext.LoanCases
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == caseId
                         && (authorizationService.IsInternalUser || x.ApplicantUserId == auth.Value),
                    ct);

            if (!exists)
                return Result<IEnumerable<LoanCaseCommentDto>>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));
        }

        var canViewInternal = authorizationService.HasPermission(LoanPermissions.ViewInternalComments);
        var filtered = comments
            .Where(x => (includeInternal && canViewInternal) || !x.IsInternal)
            .Select(dtoMapper.MapComment);

        return Result<IEnumerable<LoanCaseCommentDto>>.Ok(filtered);
    }

    private async Task<UserDisplayDto?> ResolveApplicantDisplayAsync(string applicantUserId, CancellationToken ct)
    {
        if (!authorizationService.IsInternalUser)
            return null;

        var lookup = await userDisplayLookup.GetByIdsAsync([applicantUserId], ct);
        return lookup.GetValueOrDefault(applicantUserId);
    }

    private async Task TryAutoAdvanceAfterDocumentAsync(Guid caseId, LoanDocumentType docType, CancellationToken ct)
    {
        if (docType == LoanDocumentType.FinalContract)
            await ApplyTransitionAsync(caseId, LoanWorkflowAction.UploadFinalContract, null, ct);
    }

    private async Task<Result> ApplyTransitionAsync(
        Guid caseId,
        LoanWorkflowAction action,
        string? comment,
        CancellationToken ct,
        string? internalComment = null)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        if (action == LoanWorkflowAction.RequestRevision && string.IsNullOrWhiteSpace(comment))
            return Result.Fail(Error.Validation(ApiMessages.RevisionMessageRequired));

        var actorRole = ResolveActorRole();
        var entity = await unitOfWork.LoanCases.GetScopedForTransitionAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        var statusBefore = entity.CurrentStatus;
        var phaseBefore = entity.CurrentPhase;
        var historyCountBefore = entity.WorkflowHistory.Count;
        var commentsCountBefore = entity.Comments.Count;
        var correlationId = ResolveCorrelationGuid(httpContextAccessor.HttpContext);

        var transition = await stateManager.TransitionAsync(
            entity, action, auth.Value!, actorRole, comment, correlationId);

        if (transition.IsFailure)
            return transition;

        if (!string.IsNullOrWhiteSpace(internalComment) && SupportsInternalComment(action, statusBefore))
        {
            if (!authorizationService.HasPermission(LoanPermissions.CreateInternalComment))
                return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

            entity.AddDiscussionComment(phaseBefore, auth.Value!, actorRole, internalComment, false, true);
        }

        if (entity.WorkflowHistory.Count > historyCountBefore)
        {
            var persist = await PersistTransitionAsync(entity, commentsCountBefore, ct);
            if (persist.IsFailure) return persist;

            try
            {
                await workflowOrchestrator.SignalLoanCaseAsync(
                    caseId, WorkflowSignals.StatusChanged, null, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Loan workflow signal failed for case {CaseId}", caseId);
            }
        }

        return Result.Ok();
    }

    private static bool SupportsInternalComment(LoanWorkflowAction action, LoanCaseStatus statusBefore) =>
        action switch
        {
            LoanWorkflowAction.Approve => statusBefore is
                LoanCaseStatus.PendingCreditReview or
                LoanCaseStatus.PendingLegalFinalReview or
                LoanCaseStatus.PendingFinancialReview,
            _ => false
        };

    private async Task<Result> PersistTransitionAsync(
        LoanCase entity,
        int commentsCountBefore,
        CancellationToken ct)
    {
        var history = entity.WorkflowHistory[^1];
        var pendingComments = entity.Comments.Skip(commentsCountBefore).ToList();

        if (dbContext is DbContext ef)
            ef.ChangeTracker.Clear();

        var rows = await dbContext.LoanCases.ApplyStateAsync(
            entity.Id,
            entity.CurrentStatus,
            entity.CurrentPhase,
            entity.UpdatedAt ?? clock.UtcNow,
            entity.CompletedAt,
            ct);

        if (rows == 0)
            return Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound));

        await dbContext.LoanCaseWorkflowHistories.AddAsync(history, ct);
        foreach (var c in pendingComments)
            await dbContext.LoanCaseComments.AddAsync(c, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static Result<string> ValidateAndNormalizeLoanDocumentKey(string caseNumber, string s3Key)
    {
        if (string.IsNullOrWhiteSpace(s3Key))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        s3Key = Uri.UnescapeDataString(s3Key.Trim()).Replace('\\', '/');

        if (s3Key.Contains("..", StringComparison.Ordinal))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        if (!s3Key.StartsWith($"loan-cases/{caseNumber}/", StringComparison.Ordinal))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        var parts = s3Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        if (!TryResolveLoanDocumentType(parts[2], out _))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentType));

        var file = parts[3];
        if (!int.TryParse(Path.GetFileNameWithoutExtension(file), out var ver) || ver <= 0)
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        return Result<string>.Ok(s3Key);
    }

    private static LoanDocumentType ExtractLoanDocumentTypeFromKey(string s3Key)
    {
        var parts = s3Key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 4 && TryResolveLoanDocumentType(parts[2], out var docType)
            ? docType
            : LoanDocumentType.Other;
    }

    private static int ExtractLoanDocumentVersionFromKey(string s3Key)
    {
        var parts = s3Key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return 0;

        return int.TryParse(Path.GetFileNameWithoutExtension(parts[3]), out var version) ? version : 0;
    }

    private static bool TryResolveLoanDocumentType(string segment, out LoanDocumentType docType)
    {
        docType = default;
        if (int.TryParse(segment, out var numericType)
            && Enum.IsDefined(typeof(LoanDocumentType), numericType))
        {
            docType = (LoanDocumentType)numericType;
            return true;
        }

        return Enum.TryParse(segment, ignoreCase: true, out docType);
    }

    private Result<string> RequireUser() => authorizationService.EnsureAuthenticated();

    private async Task<FundCreditCapacitySnapshotDto?> ResolveFundCreditCapacityForCaseAsync(
        LoanCaseStatus status,
        CancellationToken ct)
    {
        if (status is not (LoanCaseStatus.PendingCeoInitialApproval or LoanCaseStatus.PendingCeoFinalApproval))
            return null;

        if (!FundCreditLimitAuthorization.CanAccessFundCreditLimits(userContext.Roles))
            return null;

        return await FundCreditLimitCapacityCalculator.ComputeActiveAsync(
            dbContext,
            FundModuleType.Loan,
            DateOnly.FromDateTime(DateTime.UtcNow),
            ct);
    }

    private string ResolveActorRole()
    {
        if (userContext.Roles.Contains(UserRoleClaims.Admin)) return UserRoleClaims.Admin;
        if (userContext.Roles.Contains(UserRoleClaims.Ceo)) return UserRoleClaims.Ceo;
        if (userContext.Roles.Contains(UserRoleClaims.CreditManager)) return UserRoleClaims.CreditManager;
        if (userContext.Roles.Contains(UserRoleClaims.CreditExpert)) return UserRoleClaims.CreditExpert;
        if (userContext.Roles.Contains(UserRoleClaims.LegalManager)) return UserRoleClaims.LegalManager;
        if (userContext.Roles.Contains(UserRoleClaims.LegalExpert)) return UserRoleClaims.LegalExpert;
        if (userContext.Roles.Contains(UserRoleClaims.FinancialManager)) return UserRoleClaims.FinancialManager;
        if (userContext.Roles.Contains(UserRoleClaims.FinancialExpert)) return UserRoleClaims.FinancialExpert;
        if (userContext.Roles.Contains(UserRoleClaims.Applicant)) return UserRoleClaims.Applicant;
        return UserRoleClaims.Normalize(userContext.Roles.FirstOrDefault() ?? string.Empty);
    }

    private static Guid ResolveCorrelationGuid(HttpContext? httpContext)
    {
        var raw = httpContext?.Items[CorrelationContext.ItemKey]?.ToString()
                  ?? httpContext?.Request.Headers[CorrelationContext.HeaderName].ToString()
                  ?? httpContext?.TraceIdentifier;

        return Guid.TryParse(raw, out var parsed) ? parsed : Guid.NewGuid();
    }
}
