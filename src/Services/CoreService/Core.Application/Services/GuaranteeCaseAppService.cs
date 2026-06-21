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
using Core.Domain.Entities.Fund;
using Core.Domain.Enums;
using Core.Domain.Identity;
using Core.Domain.Identity.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public sealed class GuaranteeCaseAppService(
    ICoreUnitOfWork unitOfWork,
    ICoreDbContext dbContext,
    IGuaranteeCaseStateManager stateManager,
    IGuaranteeWorkflowOrchestrator workflowOrchestrator,
    IGuaranteeCaseNumberGenerator caseNumberGenerator,
    IDocumentStorage documentStorage,
    BuildingBlocks.Domain.Abstractions.IClock clock,
    IUserContext userContext,
    IGuaranteeAuthorizationService authorizationService,
    IGuaranteeCaseDtoMapper dtoMapper,
    IUserDisplayLookup userDisplayLookup,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GuaranteeCaseAppService> logger) : IGuaranteeCaseAppService
{
    public async Task<Result<GuaranteeCaseDto>> CreateAsync(CreateGuaranteeCaseRequest request, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<GuaranteeCaseDto>.Fail(auth.Error!);

        if (!authorizationService.HasPermission(GuaranteePermissions.Create))
            return Result<GuaranteeCaseDto>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var caseNumber = await caseNumberGenerator.GenerateGuaranteeCaseAsync(ct);
        var entity = new GuaranteeCase(caseNumber, auth.Value!, request.ApplicantType);
        Company? linkedCompany = null;

        if (request.ApplicantType == ApplicantType.Company)
        {
            if (request.CompanyId is null || request.CompanyId == Guid.Empty)
                return Result<GuaranteeCaseDto>.Fail(Error.Validation(ApiMessages.CompanyRequiredForCompanyApplicant));

            if (!Guid.TryParse(auth.Value, out var userId))
                return Result<GuaranteeCaseDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

            linkedCompany = await unitOfWork.Companies.FirstOrDefaultAsync(
                c => c.Id == request.CompanyId.Value,
                asNoTracking: true,
                cancellationToken: ct);

            if (linkedCompany is null)
                return Result<GuaranteeCaseDto>.Fail(Error.NotFound(ApiMessages.CompanyNotFound));

            if (linkedCompany.OwnerUserId != userId)
                return Result<GuaranteeCaseDto>.Fail(Error.Forbidden(ApiMessages.CompanyAccessDenied));

            entity.AssignCompany(linkedCompany.Id);
        }

        entity.SetTitle(request.Title);

        var workflowInstanceId = await workflowOrchestrator.StartGuaranteeCaseAsync(entity.Id, ct);
        entity.AttachWorkflowInstance(workflowInstanceId);

        await unitOfWork.GuaranteeCases.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<GuaranteeCaseDto>.Ok(
            dtoMapper.MapCase(entity, authorizationService.IsInternalUser, linkedCompany));
    }

    public async Task<Result<GuaranteeCaseDto>> GetAsync(Guid caseId, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<GuaranteeCaseDto>.Fail(auth.Error!);

        var detail = await unitOfWork.GuaranteeCases.GetDetailProjectionAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (detail is null)
            return Result<GuaranteeCaseDto>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        var creditSnapshot = await GuaranteeApplicantCreditSnapshotCalculator.ComputeFundSnapshotAsync(
            dbContext, currentCase: null, ct);
        var fundCreditCapacity = await ResolveFundCreditCapacityForCaseAsync(detail.CurrentStatus, ct);

        if (authorizationService.IsInternalUser
            && (string.IsNullOrWhiteSpace(detail.ApplicantFullName) ||
                string.IsNullOrWhiteSpace(detail.ApplicantPhoneNumber)))
        {
            var applicantDisplay = await ResolveApplicantDisplayAsync(detail.ApplicantUserId, ct);
            detail = detail with
            {
                ApplicantFullName = detail.ApplicantFullName ?? applicantDisplay?.FullName,
                ApplicantPhoneNumber = detail.ApplicantPhoneNumber ?? applicantDisplay?.PhoneNumber
            };
        }

        return Result<GuaranteeCaseDto>.Ok(
            dtoMapper.MapFromDetailProjection(
                detail,
                authorizationService.IsInternalUser,
                creditSnapshot,
                fundCreditCapacity));
    }

    public async Task<Result<GuaranteeCaseDto>> UpdateTitleAsync(Guid caseId, UpdateCaseTitleRequest request,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<GuaranteeCaseDto>.Fail(auth.Error!);

        var isInternal = authorizationService.IsInternalUser;
        var exists = await dbContext.GuaranteeCases
            .AsNoTracking()
            .AnyAsync(x => x.Id == caseId && (isInternal || x.ApplicantUserId == auth.Value), ct);

        if (!exists)
            return Result<GuaranteeCaseDto>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        var title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        var rows = await dbContext.GuaranteeCases.SetTitleAsync(caseId, title, clock.UtcNow, ct);

        if (rows == 0)
            return Result<GuaranteeCaseDto>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        return await GetAsync(caseId, ct);
    }

    public async Task<Result<PagedResult<GuaranteeCaseDto>>> GetPagedAsync(GetGuaranteeCasesRequest request,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<PagedResult<GuaranteeCaseDto>>.Fail(auth.Error!);

        var page = await unitOfWork.GuaranteeCases.GetPagedAsync(
            request,
            auth.Value!,
            authorizationService.IsInternalUser,
            ct);

        var items = page.Items
            .Select(x => dtoMapper.MapFromListProjection(x, authorizationService.IsInternalUser))
            .ToList();

        return Result<PagedResult<GuaranteeCaseDto>>.Ok(
            new PagedResult<GuaranteeCaseDto>(items, page.Skip, page.Take, page.TotalCount));
    }

    public async Task<Result<IEnumerable<GuaranteeWorkflowHistoryDto>>> GetHistoryAsync(Guid caseId,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<IEnumerable<GuaranteeWorkflowHistoryDto>>.Fail(auth.Error!);

        var history = await unitOfWork.GuaranteeCases.GetWorkflowHistoryAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (history.Count == 0)
        {
            var exists = await dbContext.GuaranteeCases
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == caseId
                         && (authorizationService.IsInternalUser || x.ApplicantUserId == auth.Value),
                    ct);

            if (!exists)
                return Result<IEnumerable<GuaranteeWorkflowHistoryDto>>.Fail(
                    Error.NotFound(ApiMessages.GuaranteeCaseNotFound));
        }

        return Result<IEnumerable<GuaranteeWorkflowHistoryDto>>.Ok(history.Select(dtoMapper.MapHistory));
    }

    public async Task<Result> UpdateApplicationAsync(Guid caseId, UpdateGuaranteeApplicationRequest request,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        var validation = ValidateApplicationNumericFields(request);
        if (validation.IsFailure)
            return validation;

        var isInternal = authorizationService.IsInternalUser;
        var currentStatus = await dbContext.GuaranteeCases
            .AsNoTracking()
            .Where(x => x.Id == caseId && (isInternal || x.ApplicantUserId == auth.Value))
            .Select(x => (GuaranteeCaseStatus?)x.CurrentStatus)
            .FirstOrDefaultAsync(ct);

        if (currentStatus is null)
            return Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        if (currentStatus is not (GuaranteeCaseStatus.Draft or GuaranteeCaseStatus.DataEntry))
            return Result.Fail(Error.Conflict(ApiMessages.GuaranteeApplicationNotEditable));

        if (!GuaranteeApplicationCompleteness.HasMinimumData(
                request.GuaranteeType,
                request.ContractSubject,
                request.RequestedGuaranteeAmount))
        {
            return Result.Fail(Error.Validation(ApiMessages.GuaranteeApplicationIncomplete));
        }

        var application = await GuaranteeCaseApplicationPersistence.UpsertAsync(
            dbContext, caseId, request, ct);

        await SyncApprovalFormFromApplicationAsync(caseId, application, ct);
        await dbContext.GuaranteeCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static Result ValidateApplicationNumericFields(UpdateGuaranteeApplicationRequest request)
    {
        const decimal maxPercent = 999.99m;
        const decimal maxMoney = 9999999999999999.99m;

        if (request.PriceAdjustmentRatePercent is < 0 or > maxPercent)
            return Result.Fail(
                Error.Validation("نرخ تعدیل مبلغ قرارداد باید عددی بین ۰ تا ۹۹۹٫۹۹ (درصد) باشد، نه مبلغ ریالی."));

        if (request.BaseContractAmount is < 0 or > maxMoney)
            return Result.Fail(Error.Validation("مبلغ قرارداد پایه خارج از محدوده مجاز است."));

        if (request.RequestedGuaranteeAmount is < 0 or > maxMoney)
            return Result.Fail(Error.Validation("مبلغ ضمانت‌نامه درخواستی خارج از محدوده مجاز است."));

        if (request.InitialValidityDays is < 0 or > 36500)
            return Result.Fail(Error.Validation("مدت اعتبار اولیه (روز) نامعتبر است."));

        return Result.Ok();
    }

    public Task<Result> BeginDataEntryAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Submit, null, ct);

    public Task<Result> SubmitApplicationAsync(Guid caseId, string? comment, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Submit, comment, ct);

    public Task<Result> ApproveCreditReviewAsync(Guid caseId, string? comment, string? internalComment,
        CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Approve, comment, ct, internalComment);

    public Task<Result> RequestCreditRevisionAsync(Guid caseId, string message, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.RequestRevision, message, ct);

    public Task<Result> UpdateApprovalFormAsync(Guid caseId, UpdateGuaranteeApprovalFormRequest request,
        CancellationToken ct)
    {
        return UpdateApprovalFormCoreAsync(caseId, request, ct);
    }

    public async Task<Result> SubmitApprovalFormAsync(Guid caseId, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        var entity = await unitOfWork.GuaranteeCases.GetScopedForTransitionAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        if (entity.CurrentStatus != GuaranteeCaseStatus.ApprovalFormEntry)
            return Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));

        var creditCheck = await GuaranteeFundCreditGuard.ValidateApprovalFormSubmitAsync(dbContext, entity, ct);
        if (creditCheck.IsFailure)
            return creditCheck;

        return await ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Submit, null, ct);
    }

    public Task<Result> CeoApproveInitialAsync(Guid caseId, string? comment, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Approve, comment, ct);

    public Task<Result> CeoRejectInitialAsync(Guid caseId, string reason, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Reject, reason, ct);

    public Task<Result> CeoCancelInitialAsync(Guid caseId, string reason, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Cancel, reason, ct);

    public Task<Result> CancelAsync(Guid caseId, string reason, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Cancel, reason, ct);

    public Task<Result> ConfirmDraftContractUploadedAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.UploadDraftContract, null, ct);

    public Task<Result> SubmitSignedPackageAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.SubmitSignedPackage, null, ct);

    public Task<Result> ApproveAttachmentsAsync(Guid caseId, string? comment, string? internalComment,
        CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.ApproveAttachments, comment, ct, internalComment);

    public Task<Result> RequestAttachmentRevisionAsync(Guid caseId, string message, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.RequestRevision, message, ct);

    public Task<Result> ConfirmFinalContractUploadedAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.UploadFinalContract, null, ct);

    public Task<Result> CeoApproveFinalAsync(Guid caseId, string? comment, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Approve, comment, ct);

    public Task<Result> CeoRejectOrCancelFinalAsync(Guid caseId, string reason, bool cancel, CancellationToken ct)
        => ApplyTransitionAsync(
            caseId,
            cancel ? GuaranteeWorkflowAction.Cancel : GuaranteeWorkflowAction.Reject,
            reason,
            ct);

    public Task<Result> ConfirmIssuanceDocumentsUploadedAsync(Guid caseId, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.UploadIssuanceDocuments, null, ct);

    public async Task<Result<PresignGuaranteeUploadResponse>> PresignDocumentUploadAsync(
        Guid caseId,
        PresignGuaranteeUploadRequest request,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<PresignGuaranteeUploadResponse>.Fail(auth.Error!);

        if (!authorizationService.HasPermission(GuaranteePermissions.UploadDocuments))
            return Result<PresignGuaranteeUploadResponse>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await unitOfWork.GuaranteeCases.GetScopedWithDocumentsAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<PresignGuaranteeUploadResponse>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        var version = entity.Documents
            .Where(x => !x.IsDeleted && x.DocumentType == request.DocumentType)
            .Select(x => x.Version)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var ext = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
        var s3Key = $"guarantee-cases/{entity.CaseNumber}/{(int)request.DocumentType}/{version}{ext}";

        var (url, expiresAt) =
            await documentStorage.PresignUploadAsync(s3Key, request.MimeType, TimeSpan.FromMinutes(15), ct);
        return Result<PresignGuaranteeUploadResponse>.Ok(
            new PresignGuaranteeUploadResponse(s3Key, url, expiresAt, version));
    }

    public async Task<Result<GuaranteeCaseDocumentDto>> ConfirmDocumentUploadedAsync(Guid caseId, string s3Key,
        string? originalFileName, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<GuaranteeCaseDocumentDto>.Fail(auth.Error!);

        var entity = await unitOfWork.GuaranteeCases.GetScopedWithDocumentsAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<GuaranteeCaseDocumentDto>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        var keyValidation = ValidateAndNormalizeGuaranteeDocumentKey(entity.CaseNumber, s3Key);
        if (keyValidation.IsFailure)
            return Result<GuaranteeCaseDocumentDto>.Fail(keyValidation.Error!);

        s3Key = keyValidation.Value!;

        var existing = entity.Documents.FirstOrDefault(x => string.Equals(x.S3Key, s3Key, StringComparison.Ordinal));
        if (existing is not null)
            return Result<GuaranteeCaseDocumentDto>.Ok(dtoMapper.MapDocument(existing));

        var metadata = await documentStorage.GetMetadataAsync(s3Key, ct);
        if (metadata is null || metadata.ContentLength <= 0)
            return Result<GuaranteeCaseDocumentDto>.Fail(Error.Conflict(ApiMessages.UploadedFileNotFound));

        var docType = ExtractGuaranteeDocumentTypeFromKey(s3Key);
        var version = ExtractGuaranteeDocumentVersionFromKey(s3Key);
        var mimeType = string.IsNullOrWhiteSpace(metadata.ContentType)
            ? "application/octet-stream"
            : metadata.ContentType;

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

        await dbContext.GuaranteeCaseDocuments.AddAsync(document, ct);
        await dbContext.GuaranteeCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await TryAutoAdvanceAfterDocumentAsync(caseId, docType, ct);
        return Result<GuaranteeCaseDocumentDto>.Ok(dtoMapper.MapDocument(document));
    }

    public async Task<Result<IEnumerable<GuaranteeCaseDocumentDto>>> ListDocumentsAsync(Guid caseId,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<IEnumerable<GuaranteeCaseDocumentDto>>.Fail(auth.Error!);

        var entity = await unitOfWork.GuaranteeCases.GetScopedAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<IEnumerable<GuaranteeCaseDocumentDto>>.Fail(
                Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        return Result<IEnumerable<GuaranteeCaseDocumentDto>>.Ok(
            entity.Documents.Where(x => !x.IsDeleted).Select(dtoMapper.MapDocument));
    }

    public async Task<Result<DocumentDownloadFileResult>> DownloadDocumentFileAsync(
        Guid caseId,
        Guid documentId,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result<DocumentDownloadFileResult>.Fail(auth.Error!);

        if (!authorizationService.HasPermission(GuaranteePermissions.DownloadDocuments))
            return Result<DocumentDownloadFileResult>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await unitOfWork.GuaranteeCases.GetScopedWithDocumentsAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<DocumentDownloadFileResult>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

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

    public async Task<Result<IEnumerable<GuaranteeCaseCommentDto>>> ListCommentsAsync(
        Guid caseId,
        bool includeInternal,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<IEnumerable<GuaranteeCaseCommentDto>>.Fail(auth.Error!);

        var comments = await unitOfWork.GuaranteeCases.GetCommentsAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (comments.Count == 0)
        {
            var exists = await dbContext.GuaranteeCases
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == caseId
                         && (authorizationService.IsInternalUser || x.ApplicantUserId == auth.Value),
                    ct);

            if (!exists)
                return Result<IEnumerable<GuaranteeCaseCommentDto>>.Fail(
                    Error.NotFound(ApiMessages.GuaranteeCaseNotFound));
        }

        var canViewInternal = authorizationService.HasPermission(GuaranteePermissions.ViewInternalComments);
        var filtered = comments
            .Where(x => (includeInternal && canViewInternal) || !x.IsInternal)
            .Select(dtoMapper.MapComment);

        return Result<IEnumerable<GuaranteeCaseCommentDto>>.Ok(filtered);
    }

    public async Task<Result<GuaranteeFundCreditLimitDto>> GetFundCreditLimitAsync(CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result<GuaranteeFundCreditLimitDto>.Fail(auth.Error!);

        return Result<GuaranteeFundCreditLimitDto>.Ok(await BuildFundCreditLimitDtoAsync(ct));
    }

    public async Task<Result<GuaranteeFundCreditLimitDto>> SetFundCreditLimitAsync(
        SetGuaranteeFundCreditLimitRequest request,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result<GuaranteeFundCreditLimitDto>.Fail(auth.Error!);

        if (!authorizationService.HasPermission(GuaranteePermissions.SetApplicantCreditLimit))
            return Result<GuaranteeFundCreditLimitDto>.Fail(Error.Forbidden(ApiMessages.OnlyCeoCanSetCreditLimit));

        var amountValidation = ValidateFundCreditLimitAmount(request.CreditLimitWithCheck);
        if (amountValidation.IsFailure)
            return Result<GuaranteeFundCreditLimitDto>.Fail(amountValidation.Error!);

        if (request.ExpiresAt < request.PeriodStart)
            return Result<GuaranteeFundCreditLimitDto>.Fail(Error.Validation(ApiMessages.InvalidFundCreditLimitPeriod));

        try
        {
            await UpsertFundCreditLimitAsync(
                request.CreditLimitWithCheck,
                request.PeriodStart,
                request.ExpiresAt,
                auth.Value!,
                ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNumericFieldOverflow(ex))
        {
            return Result<GuaranteeFundCreditLimitDto>.Fail(
                Error.Validation(ApiMessages.CreditLimitDatabasePrecisionTooSmall));
        }
        catch (InvalidOperationException ex) when (ex.Message == ApiMessages.FundCreditLimitPeriodOverlap)
        {
            return Result<GuaranteeFundCreditLimitDto>.Fail(Error.Conflict(ApiMessages.FundCreditLimitPeriodOverlap));
        }

        return Result<GuaranteeFundCreditLimitDto>.Ok(await BuildFundCreditLimitDtoAsync(ct));
    }

    public async Task<Result<GuaranteeFundCreditLimitDto>> SetApplicantCreditLimitAsync(
        Guid caseId,
        SetGuaranteeApplicantCreditLimitRequest request,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result<GuaranteeFundCreditLimitDto>.Fail(auth.Error!);

        if (!authorizationService.HasPermission(GuaranteePermissions.SetApplicantCreditLimit))
            return Result<GuaranteeFundCreditLimitDto>.Fail(Error.Forbidden(ApiMessages.OnlyCeoCanSetCreditLimit));

        var amountValidation = ValidateFundCreditLimitAmount(request.CreditLimitWithCheck);
        if (amountValidation.IsFailure)
            return Result<GuaranteeFundCreditLimitDto>.Fail(amountValidation.Error!);

        var entity = await unitOfWork.GuaranteeCases.GetScopedAsync(
            caseId, auth.Value!, isInternalUser: true, ct);

        if (entity is null)
            return Result<GuaranteeFundCreditLimitDto>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        await UpsertApplicantCreditProfileAsync(
            entity.ApplicantUserId,
            entity.CompanyId,
            request.CreditLimitWithCheck,
            auth.Value!,
            ct);

        await unitOfWork.SaveChangesAsync(ct);
        return Result<GuaranteeFundCreditLimitDto>.Ok(await BuildFundCreditLimitDtoAsync(ct));
    }

    public async Task<Result<GuaranteeFundCreditLimitDto>> GetApplicantCreditLimitAsync(Guid caseId,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result<GuaranteeFundCreditLimitDto>.Fail(auth.Error!);

        var entity = await unitOfWork.GuaranteeCases.GetScopedAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result<GuaranteeFundCreditLimitDto>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        var profile = await FindApplicantCreditProfileAsync(entity.ApplicantUserId, entity.CompanyId, ct);
        if (profile is null)
            return Result<GuaranteeFundCreditLimitDto>.Fail(Error.NotFound(ApiMessages.FundCreditLimitNotSet));

        return Result<GuaranteeFundCreditLimitDto>.Ok(await BuildFundCreditLimitDtoAsync(ct));
    }

    private static bool IsNumericFieldOverflow(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex.Message.Contains("numeric field overflow", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Result ValidateFundCreditLimitAmount(decimal amount)
    {
        if (amount <= 0)
            return Result.Fail(Error.Validation(ApiMessages.InvalidCreditLimitAmount));

        if (amount > GuaranteeFundCreditLimits.MaxCreditLimitWithCheck)
            return Result.Fail(Error.Validation(ApiMessages.CreditLimitAmountTooLarge));

        return Result.Ok();
    }

    private async Task UpsertFundCreditLimitAsync(
        decimal amount,
        DateOnly periodStart,
        DateOnly expiresAt,
        string setByUserId,
        CancellationToken ct)
    {
        if (await FundCreditLimitCapacityCalculator.HasOverlappingPeriodAsync(
                dbContext,
                FundModuleType.Guarantee,
                periodStart,
                expiresAt,
                excludeId: null,
                ct))
        {
            throw new InvalidOperationException(ApiMessages.FundCreditLimitPeriodOverlap);
        }

        var row = new FundCreditLimit(
            FundModuleType.Guarantee,
            amount,
            periodStart,
            expiresAt,
            setByUserId);

        await dbContext.FundCreditLimits.AddAsync(row, ct);
    }

    private async Task<FundCreditCapacitySnapshotDto?> ResolveFundCreditCapacityForCaseAsync(
        GuaranteeCaseStatus status,
        CancellationToken ct)
    {
        if (status is not (GuaranteeCaseStatus.CeoApprovalInitial or GuaranteeCaseStatus.CeoApprovalFinal))
            return null;

        if (!FundCreditLimitAuthorization.CanAccessFundCreditLimits(userContext.Roles))
            return null;

        return await FundCreditLimitCapacityCalculator.ComputeActiveAsync(
            dbContext,
            FundModuleType.Guarantee,
            DateOnly.FromDateTime(DateTime.UtcNow),
            ct);
    }

    private async Task<GuaranteeFundCreditLimitDto> BuildFundCreditLimitDtoAsync(CancellationToken ct)
    {
        var snapshot = await GuaranteeApplicantCreditSnapshotCalculator.ComputeFundSnapshotAsync(
            dbContext,
            currentCase: null,
            ct);

        var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var pool = await FundCreditLimitCapacityCalculator.ResolveActivePoolAsync(
            dbContext,
            FundModuleType.Guarantee,
            referenceDate,
            ct);

        var row = pool is null
            ? null
            : await dbContext.FundCreditLimits
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == pool.Id, ct);

        var lastSetByUserId = row?.LastSetByUserId;
        var lastSetByLookup = string.IsNullOrWhiteSpace(lastSetByUserId)
            ? null
            : (await userDisplayLookup.GetByIdsAsync([lastSetByUserId], ct)).GetValueOrDefault(lastSetByUserId);

        return dtoMapper.MapFundCreditLimit(
            snapshot,
            row?.CreditLimitWithCheck ?? snapshot.CreditLimitWithCheck ?? 0m,
            row?.PeriodStart ?? snapshot.PeriodStart ?? referenceDate,
            row?.ExpiresAt ?? snapshot.ExpiresAt ?? referenceDate,
            lastSetByUserId,
            lastSetByLookup?.FullName,
            row?.UpdatedAt ?? row?.CreatedAt);
    }

    private async Task<UserDisplayDto?> ResolveApplicantDisplayAsync(string applicantUserId, CancellationToken ct)
    {
        if (!authorizationService.IsInternalUser)
            return null;

        var lookup = await userDisplayLookup.GetByIdsAsync([applicantUserId], ct);
        return lookup.GetValueOrDefault(applicantUserId);
    }

    private async Task<Result> UpdateApprovalFormCoreAsync(
        Guid caseId,
        UpdateGuaranteeApprovalFormRequest request,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        if (!authorizationService.HasPermission(GuaranteePermissions.ManageApprovalForm))
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

        var entity = await unitOfWork.GuaranteeCases.GetScopedForTransitionAsync(
            caseId, auth.Value!, isInternalUser: true, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        if (entity.CurrentStatus != GuaranteeCaseStatus.ApprovalFormEntry)
            return Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));

        var approvalForm = await dbContext.GuaranteeApprovalForms
            .FirstOrDefaultAsync(x => x.CaseId == caseId, ct);

        if (approvalForm is null)
        {
            approvalForm = new GuaranteeApprovalForm(caseId);
            await dbContext.GuaranteeApprovalForms.AddAsync(approvalForm, ct);
        }

        var application = entity.Application
                          ?? await GuaranteeCaseApplicationPersistence.GetByCaseIdAsync(dbContext, caseId, ct);

        var creditSnapshot = await GuaranteeApplicantCreditSnapshotCalculator.ComputeAsync(dbContext, entity, ct);

        GuaranteeApprovalFormMapping.Apply(
            approvalForm,
            application,
            creditSnapshot,
            request);

        await dbContext.GuaranteeCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <summary>guarantee-cases/{caseNumber}/{documentType}/{version}.ext — documentType: عدد یا نام enum</summary>
    private static Result<string> ValidateAndNormalizeGuaranteeDocumentKey(string caseNumber, string s3Key)
    {
        if (string.IsNullOrWhiteSpace(s3Key))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        s3Key = Uri.UnescapeDataString(s3Key.Trim()).Replace('\\', '/');

        if (s3Key.Contains("..", StringComparison.Ordinal))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        if (!s3Key.StartsWith($"guarantee-cases/{caseNumber}/", StringComparison.Ordinal))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        var parts = s3Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        if (!TryResolveGuaranteeDocumentType(parts[2], out _))
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentType));

        var file = parts[3];
        if (!int.TryParse(Path.GetFileNameWithoutExtension(file), out var ver) || ver <= 0)
            return Result<string>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        return Result<string>.Ok(s3Key);
    }

    private static GuaranteeDocumentType ExtractGuaranteeDocumentTypeFromKey(string s3Key)
    {
        var parts = s3Key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 4 && TryResolveGuaranteeDocumentType(parts[2], out var docType)
            ? docType
            : GuaranteeDocumentType.Other;
    }

    private static int ExtractGuaranteeDocumentVersionFromKey(string s3Key)
    {
        var parts = s3Key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return 0;

        return int.TryParse(Path.GetFileNameWithoutExtension(parts[3]), out var version) ? version : 0;
    }

    private static bool TryResolveGuaranteeDocumentType(string segment, out GuaranteeDocumentType docType)
    {
        docType = default;
        if (int.TryParse(segment, out var numericType)
            && Enum.IsDefined(typeof(GuaranteeDocumentType), numericType))
        {
            docType = (GuaranteeDocumentType)numericType;
            return true;
        }

        return Enum.TryParse(segment, ignoreCase: true, out docType);
    }

    private async Task TryAutoAdvanceAfterDocumentAsync(Guid caseId, GuaranteeDocumentType docType,
        CancellationToken ct)
    {
        if (docType == GuaranteeDocumentType.DraftContract)
            await ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.UploadDraftContract, null, ct);
        else if (docType == GuaranteeDocumentType.FinalContract)
            await ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.UploadFinalContract, null, ct);
    }

    private async Task<Result> ApplyTransitionAsync(
        Guid caseId,
        GuaranteeWorkflowAction action,
        string? comment,
        CancellationToken ct,
        string? internalComment = null)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        if (action == GuaranteeWorkflowAction.RequestRevision && string.IsNullOrWhiteSpace(comment))
            return Result.Fail(Error.Validation(ApiMessages.RevisionMessageRequired));

        var actorRole = ResolveActorRole();
        var entity = await unitOfWork.GuaranteeCases.GetScopedForTransitionAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

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
            if (!authorizationService.HasPermission(GuaranteePermissions.CreateInternalComment))
                return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));

            entity.AddDiscussionComment(phaseBefore, auth.Value!, actorRole, internalComment, false, true);
        }

        if (entity.WorkflowHistory.Count > historyCountBefore)
        {
            var persist = await PersistTransitionAsync(entity, commentsCountBefore, ct);
            if (persist.IsFailure) return persist;

            if (entity.CurrentStatus == GuaranteeCaseStatus.ApprovalFormEntry)
            {
                var seed = await EnsureApprovalFormSeededAsync(entity, ct);
                if (seed.IsFailure) return seed;
            }

            try
            {
                await workflowOrchestrator.SignalGuaranteeCaseAsync(
                    caseId, WorkflowSignals.StatusChanged, null, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Guarantee workflow signal failed for case {CaseId}", caseId);
            }
        }

        return Result.Ok();
    }

    private async Task<Result> EnsureApprovalFormSeededAsync(GuaranteeCase entity, CancellationToken ct)
    {
        var exists = await dbContext.GuaranteeApprovalForms
            .AsNoTracking()
            .AnyAsync(x => x.CaseId == entity.Id, ct);

        if (exists)
            return Result.Ok();

        var application = entity.Application
                          ?? await GuaranteeCaseApplicationPersistence.GetByCaseIdAsync(dbContext, entity.Id, ct);

        if (application is null)
            return Result.Ok();

        var approvalForm = new GuaranteeApprovalForm(entity.Id);
        var creditSnapshot = await GuaranteeApplicantCreditSnapshotCalculator.ComputeAsync(dbContext, entity, ct);
        GuaranteeApprovalFormMapping.Apply(approvalForm, application, creditSnapshot);

        await dbContext.GuaranteeApprovalForms.AddAsync(approvalForm, ct);
        await dbContext.GuaranteeCases.TouchUpdatedAtAsync(entity.Id, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task SyncApprovalFormFromApplicationAsync(
        Guid caseId,
        GuaranteeCaseApplication application,
        CancellationToken ct)
    {
        var approvalForm = await dbContext.GuaranteeApprovalForms
            .FirstOrDefaultAsync(x => x.CaseId == caseId, ct);

        if (approvalForm is null)
            return;

        var guaranteeCase = await dbContext.GuaranteeCases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == caseId, ct);

        if (guaranteeCase is null)
            return;

        var creditSnapshot = await GuaranteeApplicantCreditSnapshotCalculator.ComputeAsync(
            dbContext,
            guaranteeCase,
            ct);

        GuaranteeApprovalFormMapping.Apply(approvalForm, application, creditSnapshot);
    }

    private async Task UpsertApplicantCreditProfileAsync(
        string applicantUserId,
        Guid? companyId,
        decimal creditLimitWithCheck,
        string setByUserId,
        CancellationToken ct)
    {
        var profile = await FindApplicantCreditProfileAsync(applicantUserId, companyId, ct);

        if (profile is null)
        {
            await dbContext.GuaranteeApplicantCreditProfiles.AddAsync(
                new GuaranteeApplicantCreditProfile(applicantUserId, companyId, creditLimitWithCheck, setByUserId),
                ct);
            return;
        }

        profile.SetCreditLimit(creditLimitWithCheck, setByUserId);
    }

    private Task<GuaranteeApplicantCreditProfile?> FindApplicantCreditProfileAsync(
        string applicantUserId,
        Guid? companyId,
        CancellationToken ct)
    {
        if (companyId.HasValue)
        {
            return dbContext.GuaranteeApplicantCreditProfiles
                .FirstOrDefaultAsync(x => x.CompanyId == companyId.Value, ct);
        }

        return dbContext.GuaranteeApplicantCreditProfiles
            .FirstOrDefaultAsync(
                x => x.ApplicantUserId == applicantUserId && x.CompanyId == null,
                ct);
    }

    private static bool SupportsInternalComment(GuaranteeWorkflowAction action, GuaranteeCaseStatus statusBefore) =>
        action switch
        {
            GuaranteeWorkflowAction.Approve => statusBefore is GuaranteeCaseStatus.CreditReview,
            GuaranteeWorkflowAction.ApproveAttachments => statusBefore == GuaranteeCaseStatus.FinancialAttachmentReview,
            _ => false
        };

    private async Task<Result> PersistTransitionAsync(
        GuaranteeCase entity,
        int commentsCountBefore,
        CancellationToken ct)
    {
        var history = entity.WorkflowHistory[^1];
        var pendingComments = entity.Comments.Skip(commentsCountBefore).ToList();

        if (dbContext is DbContext ef)
            ef.ChangeTracker.Clear();

        var rows = await dbContext.GuaranteeCases.ApplyStateAsync(
            entity.Id,
            entity.CurrentStatus,
            entity.CurrentPhase,
            entity.UpdatedAt ?? clock.UtcNow,
            entity.CompletedAt,
            ct);

        if (rows == 0)
            return Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        await dbContext.GuaranteeCaseWorkflowHistories.AddAsync(history, ct);
        foreach (var c in pendingComments)
            await dbContext.GuaranteeCaseComments.AddAsync(c, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private Result<string> RequireUser() => authorizationService.EnsureAuthenticated();

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
        return userContext.Roles.FirstOrDefault() ?? string.Empty;
    }

    private static Guid ResolveCorrelationGuid(HttpContext? httpContext)
    {
        var raw = httpContext?.Items[CorrelationContext.ItemKey]?.ToString()
                  ?? httpContext?.Request.Headers[CorrelationContext.HeaderName].ToString()
                  ?? httpContext?.TraceIdentifier;

        return Guid.TryParse(raw, out var parsed) ? parsed : Guid.NewGuid();
    }
}
