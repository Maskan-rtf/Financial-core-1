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
using Core.Application.Notifications.Sms;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
    IServiceScopeFactory serviceScopeFactory,
    IWorkflowSmsNotifier workflowSmsNotifier,
    ILogger<GuaranteeCaseAppService> logger) : IGuaranteeCaseAppService
{
    public async Task<Result<GuaranteeCaseDto>> CreateAsync(CreateGuaranteeCaseRequest request, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure) return Result<GuaranteeCaseDto>.Fail(auth.Error!);

        if (!authorizationService.HasPermission(GuaranteePermissions.Create))
            return Result<GuaranteeCaseDto>.Fail(Error.Forbidden(ApiMessages.NotAllowed));

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
        }

        for (var sequence = 1; sequence <= CaseNumberFormat.MaxDailySequenceAttempts; sequence++)
        {
            var caseNumber = await caseNumberGenerator.GenerateGuaranteeCaseAsync(ct, sequence);
            var entity = new GuaranteeCase(caseNumber, auth.Value!, request.ApplicantType);

            if (linkedCompany is not null)
                entity.AssignCompany(linkedCompany.Id);

            entity.SetTitle(request.Title);

            var workflowInstanceId = await workflowOrchestrator.StartGuaranteeCaseAsync(entity.Id, ct);
            entity.AttachWorkflowInstance(workflowInstanceId);

            await unitOfWork.GuaranteeCases.AddAsync(entity, ct);
            try
            {
                await unitOfWork.SaveChangesAsync(ct);
                return Result<GuaranteeCaseDto>.Ok(
                    dtoMapper.MapCase(entity, authorizationService.IsInternalUser, linkedCompany));
            }
            catch (DbUpdateException)
            {
                // Unique constraint on CaseNumber could collide; retry with next daily sequence.
            }
        }

        return Result<GuaranteeCaseDto>.Fail(Error.Unexpected(ApiMessages.CaseNumberAllocationFailed));
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

    public async Task<Result<GuaranteeAmendmentDto>> GetAmendmentAsync(Guid caseId, CancellationToken ct)
    {
        var scoped = await GetScopedCaseAsync(caseId, ct);
        if (scoped.IsFailure)
            return Result<GuaranteeAmendmentDto>.Fail(scoped.Error!);

        var entity = scoped.Value!;

        var history = await LoadAmendmentHistoryAsync(caseId, ct);
        var amendment = BuildAmendmentDto(entity, history);
        if (amendment is null)
        {
            if (history.Count == 0)
                return Result<GuaranteeAmendmentDto>.Fail(Error.NotFound(ApiMessages.InvalidTransition));

            var latest = history.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
            return Result<GuaranteeAmendmentDto>.Ok(new GuaranteeAmendmentDto(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                latest?.PreviousValues,
                latest?.NewValues,
                history));
        }

        return Result<GuaranteeAmendmentDto>.Ok(amendment);
    }

    public async Task<Result<GuaranteeCancellationDetailsDto>> GetCancellationDetailsAsync(Guid caseId, CancellationToken ct)
    {
        var scoped = await GetScopedCaseAsync(caseId, ct);
        if (scoped.IsFailure)
            return Result<GuaranteeCancellationDetailsDto>.Fail(scoped.Error!);

        return Result<GuaranteeCancellationDetailsDto>.Ok(BuildCancellationDetailsDto(scoped.Value!));
    }

    public async Task<Result<GuaranteeAmendmentDto>> CreateOrUpdateAmendmentAsync(
        Guid caseId,
        CreateGuaranteeAmendmentRequest request,
        CancellationToken ct)
    {
        var scoped = await GetScopedCaseForTransitionAsync(caseId, ct);
        if (scoped.IsFailure)
            return Result<GuaranteeAmendmentDto>.Fail(scoped.Error!);

        var entity = scoped.Value!;

        if (entity.CurrentStatus is not (
                GuaranteeCaseStatus.Completed or
                GuaranteeCaseStatus.AmendmentApproved or
                GuaranteeCaseStatus.AmendmentRejected or
                GuaranteeCaseStatus.AmendmentDraft or
                GuaranteeCaseStatus.AmendmentDataEntry))
        {
            return Result<GuaranteeAmendmentDto>.Fail(Error.Conflict(GuaranteeAmendmentMessages.NotEditable));
        }

        if (entity.CurrentStatus == GuaranteeCaseStatus.Cancelled)
            return Result<GuaranteeAmendmentDto>.Fail(Error.Conflict(ApiMessages.GuaranteeCancellationNotEligible));

        if (HasActiveAmendmentWorkflow(entity) && entity.AmendmentType != request.AmendmentType
            && entity.CurrentStatus is not (GuaranteeCaseStatus.AmendmentDraft or GuaranteeCaseStatus.AmendmentDataEntry))
        {
            return Result<GuaranteeAmendmentDto>.Fail(Error.Conflict(ApiMessages.GuaranteeCancellationAlreadyActive));
        }

        var fieldValidation = ValidateAmendmentRequest(request);
        if (fieldValidation.IsFailure)
            return Result<GuaranteeAmendmentDto>.Fail(fieldValidation.Error!);

        entity.UpsertAmendment(request.AmendmentType, request.NewValidityTo, request.NewGuaranteeAmount, request.Reason);

        if (entity.CurrentStatus is GuaranteeCaseStatus.Completed or GuaranteeCaseStatus.AmendmentApproved or GuaranteeCaseStatus.AmendmentRejected)
        {
            var start = await ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.BeginAmendment, request.Reason, ct);
            if (start.IsFailure)
                return Result<GuaranteeAmendmentDto>.Fail(start.Error!);
        }
        else
        {
            await dbContext.GuaranteeCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        var history = await LoadAmendmentHistoryAsync(caseId, ct);
        return Result<GuaranteeAmendmentDto>.Ok(BuildAmendmentDto(entity, history)!);
    }

    public async Task<Result> CreateCancellationAsync(Guid caseId, CreateGuaranteeCancellationRequest request, CancellationToken ct)
    {
        var prepared = await PrepareCancellationDraftAsync(
            caseId,
            request.Reason,
            request.OriginalGuaranteeReference,
            request.SettlementConfirmationRequired,
            ct,
            validateEligibility: true);
        if (prepared.IsFailure)
            return Result.Fail(prepared.Error!);

        var entity = prepared.Value!;

        if (entity.CurrentStatus is GuaranteeCaseStatus.Completed or GuaranteeCaseStatus.AmendmentApproved)
            return await ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.BeginAmendment, request.Reason, ct);

        await PersistDraftChangesAsync(caseId, ct);
        return Result.Ok();
    }

    public Task<Result> SubmitAmendmentAsync(Guid caseId, CancellationToken ct)
    {
        // #region agent log
        AgentDebugLog.Write("H4", "GuaranteeCaseAppService.SubmitAmendmentAsync", "entry", new { caseId });
        // #endregion
        return SubmitCurrentAmendmentAsync(caseId, null, ct);
    }

    public async Task<Result> SubmitCancellationAsync(Guid caseId, SubmitGuaranteeCancellationRequest request, CancellationToken ct)
    {
        var prepared = await PrepareCancellationDraftAsync(
            caseId,
            reason: null,
            request.OriginalGuaranteeReference,
            request.SettlementConfirmationRequired,
            ct,
            validateEligibility: false);
        if (prepared.IsFailure)
            return Result.Fail(prepared.Error!);

        var entity = prepared.Value!;

        if (entity.CurrentStatus == GuaranteeCaseStatus.AmendmentDraft)
        {
            var beginEntry = await SubmitCurrentAmendmentAsync(caseId, request.Comment, ct);
            if (beginEntry.IsFailure)
                return beginEntry;
        }

        return await SubmitCurrentAmendmentAsync(caseId, request.Comment, ct);
    }

    public Task<Result> ApproveAmendmentAsync(Guid caseId, string? comment, string? internalComment, CancellationToken ct)
        => ApproveCurrentAmendmentAsync(caseId, comment, internalComment, legalOverrideActiveObligationCheck: false, ct);

    public Task<Result> CeoApproveAmendmentAsync(Guid caseId, string? comment, CancellationToken ct)
        => ApproveCurrentAmendmentAsync(caseId, comment, internalComment: null, legalOverrideActiveObligationCheck: false, ct);

    public Task<Result> CeoRejectAmendmentAsync(Guid caseId, string reason, CancellationToken ct)
        => RejectCurrentAmendmentAsync(caseId, reason, ct);

    public Task<Result> ApproveCancellationAsync(Guid caseId, ApproveGuaranteeCancellationRequest request, CancellationToken ct)
        => ApproveCurrentAmendmentAsync(
            caseId,
            request.Comment,
            request.InternalComment,
            request.LegalOverrideActiveObligationCheck,
            ct);

    public Task<Result> RejectAmendmentAsync(Guid caseId, string reason, CancellationToken ct)
        => RejectCurrentAmendmentAsync(caseId, reason, ct);

    public Task<Result> RequestAmendmentRevisionAsync(Guid caseId, string message, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.RequestRevision, message, ct);

    private async Task<Result<GuaranteeCase>> GetScopedCaseAsync(Guid caseId, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result<GuaranteeCase>.Fail(auth.Error!);

        var entity = await unitOfWork.GuaranteeCases.GetScopedAsync(
            caseId,
            auth.Value!,
            authorizationService.IsInternalUser,
            ct);

        return entity is null
            ? Result<GuaranteeCase>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound))
            : Result<GuaranteeCase>.Ok(entity);
    }

    private async Task<Result<GuaranteeCase>> GetScopedCaseForTransitionAsync(Guid caseId, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result<GuaranteeCase>.Fail(auth.Error!);

        var entity = await unitOfWork.GuaranteeCases.GetScopedForTransitionAsync(
            caseId,
            auth.Value!,
            authorizationService.IsInternalUser,
            ct);

        return entity is null
            ? Result<GuaranteeCase>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound))
            : Result<GuaranteeCase>.Ok(entity);
    }

    private GuaranteeCancellationDetailsDto BuildCancellationDetailsDto(GuaranteeCase entity)
    {
        var documents = entity.Documents
            .Where(d => !d.IsDeleted)
            .Select(dtoMapper.MapDocument)
            .ToList();

        return new GuaranteeCancellationDetailsDto(
            entity.Id,
            entity.CaseNumber,
            entity.CurrentStatus,
            entity.AmendmentType,
            entity.AmendmentReason,
            entity.AmendmentOriginalGuaranteeReference ?? entity.CaseNumber,
            entity.SettlementConfirmationRequired,
            entity.AmendmentRequiresCreditReview,
            entity.LegalOverrideApproved,
            entity.AmendmentCreatedAt,
            entity.AmendmentCompletedAt,
            GuaranteeCancellationSource.BuildSnapshot(entity),
            documents);
    }

    private async Task<Result<GuaranteeCase>> PrepareCancellationDraftAsync(
        Guid caseId,
        string? reason,
        string? originalGuaranteeReference,
        bool settlementConfirmationRequired,
        CancellationToken ct,
        bool validateEligibility)
    {
        var scoped = await GetScopedCaseForTransitionAsync(caseId, ct);
        if (scoped.IsFailure)
            return scoped;

        var entity = scoped.Value!;
        if (entity.CurrentStatus == GuaranteeCaseStatus.Cancelled)
            return Result<GuaranteeCase>.Fail(Error.Conflict(ApiMessages.GuaranteeCancellationNotEligible));

        if (validateEligibility
            && entity.CurrentStatus is not (
                GuaranteeCaseStatus.Completed or
                GuaranteeCaseStatus.AmendmentApproved or
                GuaranteeCaseStatus.AmendmentDraft or
                GuaranteeCaseStatus.AmendmentDataEntry))
        {
            return Result<GuaranteeCase>.Fail(Error.Conflict(ApiMessages.GuaranteeCancellationNotEligible));
        }

        if (validateEligibility && HasActiveAmendmentWorkflow(entity) && entity.AmendmentType != AmendmentType.Cancellation)
            return Result<GuaranteeCase>.Fail(Error.Conflict(ApiMessages.GuaranteeCancellationAlreadyActive));

        entity.ConfigureCancellationAmendment(
            GuaranteeCancellationSource.ResolveReference(entity),
            GuaranteeCancellationSource.RequiresSettlementConfirmation(entity),
            HasActiveCancellationObligation(entity),
            reason ?? entity.AmendmentReason);

        return Result<GuaranteeCase>.Ok(entity);
    }

    private async Task PersistDraftChangesAsync(Guid caseId, CancellationToken ct)
    {
        await dbContext.GuaranteeCases.TouchUpdatedAtAsync(caseId, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private Task<Result> SubmitCurrentAmendmentAsync(Guid caseId, string? comment, CancellationToken ct)
        => ApplyTransitionAsync(
            caseId,
            GuaranteeWorkflowAction.Submit,
            comment,
            ct,
            beforeTransition: entity =>
            {
                if (entity.CurrentStatus != GuaranteeCaseStatus.AmendmentDataEntry)
                    return Result.Ok();

                if (entity.AmendmentType == AmendmentType.Cancellation)
                {
                    entity.ConfigureCancellationAmendment(
                        GuaranteeCancellationSource.ResolveReference(entity),
                        GuaranteeCancellationSource.RequiresSettlementConfirmation(entity),
                        HasActiveCancellationObligation(entity),
                        entity.AmendmentReason);
                }

                if (entity.AmendmentType is not (AmendmentType.Extension or AmendmentType.Reduction or AmendmentType.Cancellation))
                    return Result.Ok();

                dbContext.GuaranteeAmendmentHistoryRecords.Add(
                    BuildPendingAmendmentHistoryRecord(entity, authUserId: entity.ApplicantUserId));

                return Result.Ok();
            });

    private async Task<Result> ApproveCurrentAmendmentAsync(
        Guid caseId,
        string? comment,
        string? internalComment,
        bool legalOverrideActiveObligationCheck,
        CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result.Fail(auth.Error!);

        var scoped = await GetScopedCaseForTransitionAsync(caseId, ct);
        if (scoped.IsFailure)
            return Result.Fail(scoped.Error!);

        var entity = scoped.Value!;
        if (entity.CurrentStatus is not (
                GuaranteeCaseStatus.AmendmentCreditReview or
                GuaranteeCaseStatus.AmendmentCeoApproval or
                GuaranteeCaseStatus.AmendmentLegalReview))
            return Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));

        if (entity.AmendmentType == AmendmentType.Cancellation
            && entity.CurrentStatus == GuaranteeCaseStatus.AmendmentLegalReview
            && legalOverrideActiveObligationCheck)
        {
            entity.ApproveCancellationLegalOverride();
        }

        Result validation = Result.Ok();
        if (entity.CurrentStatus == GuaranteeCaseStatus.AmendmentCreditReview)
        {
            validation = entity.AmendmentType switch
            {
                AmendmentType.Extension => await GuaranteeFundCreditGuard.ValidateAmendmentExtensionAsync(dbContext, entity, ct),
                AmendmentType.Reduction => await GuaranteeFundCreditGuard.ValidateApprovalFormSubmitAsync(dbContext, entity, ct),
                AmendmentType.Cancellation => Result.Ok(),
                _ => Result.Fail(Error.Conflict(ApiMessages.InvalidTransition))
            };
        }
        else if (entity.AmendmentType != AmendmentType.Cancellation
                 && entity.AmendmentType is not (AmendmentType.Extension or AmendmentType.Reduction))
        {
            validation = Result.Fail(Error.Conflict(ApiMessages.InvalidTransition));
        }

        if (validation.IsFailure)
            return validation;

        if (entity.AmendmentType is AmendmentType.Extension or AmendmentType.Reduction or AmendmentType.Cancellation
            && ShouldMarkAmendmentAuditApproved(entity.CurrentStatus, entity.AmendmentType))
        {
            var auditGate = await ValidateAmendmentAuditForApprovalAsync(entity, ct);
            if (auditGate.IsFailure)
                return auditGate;
        }

        return await ApplyTransitionAsync(
            caseId,
            GuaranteeWorkflowAction.Approve,
            comment,
            ct,
            internalComment);
    }

    private async Task<Result> RejectCurrentAmendmentAsync(Guid caseId, string reason, CancellationToken ct)
    {
        var auth = RequireUser();
        if (auth.IsFailure)
            return Result.Fail(auth.Error!);

        var scoped = await GetScopedCaseForTransitionAsync(caseId, ct);
        if (scoped.IsFailure)
            return Result.Fail(scoped.Error!);

        var entity = scoped.Value!;
        if (entity.AmendmentType is AmendmentType.Extension or AmendmentType.Reduction or AmendmentType.Cancellation)
        {
            var auditGate = await ValidateAmendmentAuditForRejectAsync(entity, ct);
            if (auditGate.IsFailure)
                return auditGate;
        }

        return await ApplyTransitionAsync(
            caseId,
            GuaranteeWorkflowAction.Reject,
            reason,
            ct);
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
        => ApplyCancellationAliasAsync(caseId, reason, ct);

    public Task<Result> CancelAsync(Guid caseId, string reason, CancellationToken ct)
        => ApplyCancellationAliasAsync(caseId, reason, ct);

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
        => cancel
            ? ApplyCancellationAliasAsync(caseId, reason, ct)
            : ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Reject, reason, ct);

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
        string? internalComment = null,
        Func<GuaranteeCase, Result>? beforeTransition = null)
    {
        // #region agent log
        AgentDebugLog.Write("H3", "GuaranteeCaseAppService.ApplyTransitionAsync", "entry", new { caseId, action = action.ToString() });
        // #endregion
        var auth = RequireUser();
        if (auth.IsFailure) return Result.Fail(auth.Error!);

        if (action == GuaranteeWorkflowAction.RequestRevision && string.IsNullOrWhiteSpace(comment))
            return Result.Fail(Error.Validation(ApiMessages.RevisionMessageRequired));

        var actorRole = ResolveActorRole();
        // #region agent log
        AgentDebugLog.Write("H3", "GuaranteeCaseAppService.ApplyTransitionAsync", "before-load", new { caseId, action = action.ToString() });
        // #endregion
        var entity = await unitOfWork.GuaranteeCases.GetScopedForTransitionAsync(
            caseId, auth.Value!, authorizationService.IsInternalUser, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        // #region agent log
        AgentDebugLog.Write("H3", "GuaranteeCaseAppService.ApplyTransitionAsync", "after-load", new { caseId, status = (int)entity.CurrentStatus });
        // #endregion

        var statusBefore = entity.CurrentStatus;
        var phaseBefore = entity.CurrentPhase;
        var historyCountBefore = entity.WorkflowHistory.Count;
        var commentsCountBefore = entity.Comments.Count;
        var correlationId = ResolveCorrelationGuid(httpContextAccessor.HttpContext);

        if (beforeTransition is not null)
        {
            var preparation = beforeTransition(entity);
            if (preparation.IsFailure)
                return preparation;
        }

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

        if (entity.CurrentStatus is GuaranteeCaseStatus.AmendmentApproved or GuaranteeCaseStatus.Cancelled)
            entity.ApplyApprovedAmendment();

        if (entity.WorkflowHistory.Count > historyCountBefore)
        {
            // #region agent log
            AgentDebugLog.Write("H1", "GuaranteeCaseAppService.ApplyTransitionAsync", "before-persist", new { caseId, from = statusBefore.ToString(), to = entity.CurrentStatus.ToString() });
            // #endregion
            var persist = await PersistTransitionAsync(entity, commentsCountBefore, historyCountBefore, ct);
            if (persist.IsFailure) return persist;
            // #region agent log
            AgentDebugLog.Write("H1", "GuaranteeCaseAppService.ApplyTransitionAsync", "after-persist", new { caseId });
            // #endregion

            foreach (var historyEntry in entity.WorkflowHistory.Skip(historyCountBefore))
            {
                WorkflowSmsBackgroundNotifier.NotifyGuaranteeStepChange(
                    serviceScopeFactory,
                    logger,
                    entity.Id,
                    entity.ApplicantUserId,
                    entity.CaseNumber,
                    (int)historyEntry.FromStatus,
                    (int)historyEntry.ToStatus);
            }

            if (entity.CurrentStatus == GuaranteeCaseStatus.ApprovalFormEntry)
            {
                var seed = await EnsureApprovalFormSeededAsync(entity, ct);
                if (seed.IsFailure) return seed;
            }

            GuaranteeWorkflowBackgroundSignaler.SignalStatusChanged(serviceScopeFactory, logger, caseId);
        }

        // #region agent log
        AgentDebugLog.Write("H3", "GuaranteeCaseAppService.ApplyTransitionAsync", "exit", new { caseId, action = action.ToString(), status = entity.CurrentStatus.ToString() });
        // #endregion
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
            GuaranteeWorkflowAction.Approve => statusBefore is GuaranteeCaseStatus.CreditReview or GuaranteeCaseStatus.AmendmentCreditReview or GuaranteeCaseStatus.AmendmentCeoApproval or GuaranteeCaseStatus.AmendmentLegalReview,
            GuaranteeWorkflowAction.ApproveAttachments => statusBefore == GuaranteeCaseStatus.FinancialAttachmentReview,
            _ => false
        };

    private static bool HasActiveCancellationObligation(GuaranteeCase entity)
        => (entity.ApprovalForm?.ActiveCommitments ?? 0m) > 0m;

    private Task<Result> ApplyCancellationAliasAsync(Guid caseId, string reason, CancellationToken ct)
        => ApplyTransitionAsync(caseId, GuaranteeWorkflowAction.Cancel, reason, ct);

    private async Task<Result> PersistTransitionAsync(
        GuaranteeCase entity,
        int commentsCountBefore,
        int historyCountBefore,
        CancellationToken ct)
    {
        // #region agent log
        AgentDebugLog.Write("H1", "GuaranteeCaseAppService.PersistTransitionAsync", "entry", new { caseId = entity.Id, status = entity.CurrentStatus.ToString() });
        // #endregion
        var pendingHistory = entity.WorkflowHistory.Skip(historyCountBefore).ToList();
        var pendingComments = entity.Comments.Skip(commentsCountBefore).ToList();
        var pendingNewAmendmentRecords = CapturePendingNewAmendmentHistory();

        if (dbContext is DbContext ef)
            ef.ChangeTracker.Clear();

        foreach (var record in pendingNewAmendmentRecords)
        {
            if (dbContext is DbContext db)
                await db.InsertAmendmentHistoryRecordAsync(record, ct);
        }

        // #region agent log
        AgentDebugLog.Write("H1", "GuaranteeCaseAppService.PersistTransitionAsync", "before-audit-update", new { caseId = entity.Id });
        // #endregion
        foreach (var history in pendingHistory)
        {
            var auditApplied = await ApplyAmendmentAuditDecisionAsync(entity, history, ct);
            if (auditApplied.IsFailure)
                return auditApplied;
        }

        // #region agent log
        AgentDebugLog.Write("H1", "GuaranteeCaseAppService.PersistTransitionAsync", "before-state-update", new { caseId = entity.Id });
        // #endregion
        var rows = await dbContext.GuaranteeCases.ApplyStateAndAmendmentAsync(
            entity.Id,
            entity.CurrentStatus,
            entity.CurrentPhase,
            entity.UpdatedAt ?? clock.UtcNow,
            entity.CompletedAt,
            entity.AmendmentType,
            entity.AmendmentReason,
            entity.AmendmentRequiresCreditReview,
            entity.AmendmentOriginalGuaranteeReference,
            entity.LegalOverrideApproved,
            entity.SettlementConfirmationRequired,
            entity.AmendmentRequestedValidityTo,
            entity.AmendmentRequestedAmount,
            entity.AmendmentApprovedValidityTo,
            entity.AmendmentApprovedAmount,
            entity.AmendmentCreatedAt,
            entity.AmendmentCompletedAt,
            ct);

        if (rows == 0)
            return Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        // #region agent log
        AgentDebugLog.Write("H1", "GuaranteeCaseAppService.PersistTransitionAsync", "after-state-update", new { caseId = entity.Id, rows });
        // #endregion

        if (entity.CurrentStatus is GuaranteeCaseStatus.AmendmentApproved
            || (entity.CurrentStatus == GuaranteeCaseStatus.Cancelled && entity.AmendmentType == AmendmentType.Cancellation))
        {
            // #region agent log
            AgentDebugLog.Write("H1", "GuaranteeCaseAppService.PersistTransitionAsync", "before-approved-data", new { caseId = entity.Id });
            // #endregion
            await PersistApprovedAmendmentDataAsync(entity, ct);
            // #region agent log
            AgentDebugLog.Write("H1", "GuaranteeCaseAppService.PersistTransitionAsync", "after-approved-data", new { caseId = entity.Id });
            // #endregion
        }

        // #region agent log
        AgentDebugLog.Write("H1", "GuaranteeCaseAppService.PersistTransitionAsync", "before-history-insert", new { caseId = entity.Id });
        // #endregion
        if (dbContext is DbContext dbContextImpl)
        {
            foreach (var history in pendingHistory)
                await dbContextImpl.InsertWorkflowHistoryAsync(history, ct);
            foreach (var comment in pendingComments)
                await dbContextImpl.InsertCommentAsync(comment, ct);
        }

        // #region agent log
        AgentDebugLog.Write("H1", "GuaranteeCaseAppService.PersistTransitionAsync", "exit", new { caseId = entity.Id });
        // #endregion
        return Result.Ok();
    }

    private async Task<Result> ApplyAmendmentAuditDecisionAsync(
        GuaranteeCase entity,
        GuaranteeCaseWorkflowHistory history,
        CancellationToken ct)
    {
        if (entity.AmendmentType is not (AmendmentType.Extension or AmendmentType.Reduction or AmendmentType.Cancellation))
            return Result.Ok();

        GuaranteeAmendmentHistoryStatus? decisionStatus = history.Action switch
        {
            nameof(GuaranteeWorkflowAction.Approve)
                when ShouldMarkAmendmentAuditApproved(history.FromStatus, entity.AmendmentType)
                => GuaranteeAmendmentHistoryStatus.Approved,
            nameof(GuaranteeWorkflowAction.Reject) => GuaranteeAmendmentHistoryStatus.Rejected,
            _ => null
        };

        if (decisionStatus is null)
            return Result.Ok();

        var rows = await dbContext.GuaranteeAmendmentHistoryRecords.ApplyLatestPendingAmendmentAuditDecisionAsync(
            entity.Id,
            decisionStatus.Value,
            history.ChangedByUserId,
            history.CreatedAt,
            history.Comment,
            ct);

        if (rows == 0)
        {
            var latestStatus = await dbContext.GuaranteeAmendmentHistoryRecords
                .AsNoTracking()
                .Where(x => x.GuaranteeCaseId == entity.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (GuaranteeAmendmentHistoryStatus?)x.Status)
                .FirstOrDefaultAsync(ct);

            if (latestStatus == decisionStatus.Value)
                return Result.Ok();

            // #region agent log
            AgentDebugLog.Write("H1", "GuaranteeCaseAppService.ApplyAmendmentAuditDecisionAsync", "no-pending-audit", new { caseId = entity.Id, expected = decisionStatus.Value.ToString(), latest = latestStatus?.ToString() });
            // #endregion
            return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.Incomplete));
        }

        return Result.Ok();
    }

    private IReadOnlyList<GuaranteeAmendmentHistoryRecord> CapturePendingNewAmendmentHistory()
    {
        if (dbContext is not DbContext ef)
            return [];

        return ef.ChangeTracker
            .Entries<GuaranteeAmendmentHistoryRecord>()
            .Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity)
            .ToList();
    }

    private async Task PersistApprovedAmendmentDataAsync(GuaranteeCase entity, CancellationToken ct)
    {
        var approvedValidityTo = entity.AmendmentApprovedValidityTo ?? entity.AmendmentRequestedValidityTo;
        var approvedAmount = entity.AmendmentApprovedAmount ?? entity.AmendmentRequestedAmount;

        if (entity.AmendmentType == AmendmentType.Extension && approvedValidityTo.HasValue)
        {
            await dbContext.GuaranteeCaseApplications
                .Where(x => x.CaseId == entity.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.ValidityTo, approvedValidityTo)
                        .SetProperty(x => x.UpdatedAt, entity.UpdatedAt ?? clock.UtcNow),
                    ct);

            await dbContext.GuaranteeApprovalForms
                .Where(x => x.CaseId == entity.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.ExpiryDate, approvedValidityTo)
                        .SetProperty(
                            x => x.ActiveDurationDays,
                            x => x.IssuanceDate.HasValue
                                ? approvedValidityTo!.Value.DayNumber - x.IssuanceDate.Value.DayNumber + 1
                                : x.ActiveDurationDays)
                        .SetProperty(x => x.UpdatedAt, entity.UpdatedAt ?? clock.UtcNow),
                    ct);
        }

        if (entity.AmendmentType == AmendmentType.Reduction && approvedAmount.HasValue)
        {
            await dbContext.GuaranteeCaseApplications
                .Where(x => x.CaseId == entity.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.RequestedGuaranteeAmount, approvedAmount)
                        .SetProperty(x => x.UpdatedAt, entity.UpdatedAt ?? clock.UtcNow),
                    ct);

            await dbContext.GuaranteeApprovalForms
                .Where(x => x.CaseId == entity.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.GuaranteeAmount, approvedAmount)
                        .SetProperty(x => x.UpdatedAt, entity.UpdatedAt ?? clock.UtcNow),
                    ct);
        }

        if (entity.AmendmentType == AmendmentType.Cancellation)
        {
            await dbContext.GuaranteeApprovalForms
                .Where(x => x.CaseId == entity.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.ActiveCommitments, 0m)
                        .SetProperty(x => x.GuaranteeAmount, 0m)
                        .SetProperty(x => x.UpdatedAt, entity.UpdatedAt ?? clock.UtcNow),
                    ct);

            await dbContext.GuaranteeCaseApplications
                .Where(x => x.CaseId == entity.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.RequestedGuaranteeAmount, 0m)
                        .SetProperty(x => x.UpdatedAt, entity.UpdatedAt ?? clock.UtcNow),
                    ct);
        }
    }

    private static bool HasActiveAmendmentWorkflow(GuaranteeCase entity)
        => entity.CurrentStatus is
            GuaranteeCaseStatus.AmendmentDraft or
            GuaranteeCaseStatus.AmendmentDataEntry or
            GuaranteeCaseStatus.AmendmentCreditReview or
            GuaranteeCaseStatus.AmendmentCeoApproval or
            GuaranteeCaseStatus.AmendmentLegalReview;

    private static bool ShouldMarkAmendmentAuditApproved(GuaranteeCaseStatus status, AmendmentType? amendmentType)
        => status switch
        {
            GuaranteeCaseStatus.AmendmentLegalReview when amendmentType is AmendmentType.Extension or AmendmentType.Reduction or AmendmentType.Cancellation => true,
            _ => false
        };

    private static Result ValidateAmendmentRequest(CreateGuaranteeAmendmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Fail(Error.Validation(GuaranteeAmendmentMessages.Incomplete));

        if (request.AmendmentType == AmendmentType.Extension && request.NewGuaranteeAmount.HasValue)
            return Result.Fail(Error.Validation(GuaranteeAmendmentMessages.ReductionAmountInvalid));

        if (request.AmendmentType == AmendmentType.Reduction && request.NewValidityTo.HasValue)
            return Result.Fail(Error.Validation(GuaranteeAmendmentMessages.ExtensionDateMustExtend));

        return Result.Ok();
    }

    private async Task<Result> ValidateAmendmentAuditForApprovalAsync(GuaranteeCase entity, CancellationToken ct)
    {
        var latestStatus = await GetLatestAmendmentHistoryStatusAsync(entity.Id, ct);
        // #region agent log
        AgentDebugLog.Write("H7", "GuaranteeCaseAppService.ValidateAmendmentAuditForApprovalAsync", "audit-status", new { caseId = entity.Id, status = entity.CurrentStatus.ToString(), latest = latestStatus?.ToString() });
        // #endregion

        if (latestStatus is null)
            return await EnsureAmendmentAuditRecordAsync(entity, ct);

        if (latestStatus is GuaranteeAmendmentHistoryStatus.PendingReview or GuaranteeAmendmentHistoryStatus.Approved)
            return Result.Ok();

        return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.Incomplete));
    }

    private async Task<Result> ValidateAmendmentAuditForRejectAsync(GuaranteeCase entity, CancellationToken ct)
    {
        var latestStatus = await GetLatestAmendmentHistoryStatusAsync(entity.Id, ct);
        if (latestStatus is GuaranteeAmendmentHistoryStatus.PendingReview or GuaranteeAmendmentHistoryStatus.Rejected)
            return Result.Ok();

        if (latestStatus is null)
            return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.Incomplete));

        return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.Incomplete));
    }

    private async Task<Result> EnsureAmendmentAuditRecordAsync(GuaranteeCase entity, CancellationToken ct)
    {
        if (entity.AmendmentType is not (AmendmentType.Extension or AmendmentType.Reduction or AmendmentType.Cancellation))
            return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.Incomplete));

        if (!GuaranteeAmendmentCompleteness.IsComplete(entity))
            return Result.Fail(Error.Conflict(GuaranteeAmendmentMessages.Incomplete));

        if (dbContext is not DbContext db)
            return Result.Fail(Error.Unexpected(ApiMessages.GuaranteeCaseNotFound));

        var record = BuildPendingAmendmentHistoryRecord(entity, entity.ApplicantUserId);
        await db.InsertAmendmentHistoryRecordAsync(record, ct);
        // #region agent log
        AgentDebugLog.Write("H7", "GuaranteeCaseAppService.EnsureAmendmentAuditRecordAsync", "backfilled", new { caseId = entity.Id, recordId = record.Id });
        // #endregion
        return Result.Ok();
    }

    private async Task<GuaranteeAmendmentHistoryStatus?> GetLatestAmendmentHistoryStatusAsync(
        Guid caseId,
        CancellationToken ct)
        => await dbContext.GuaranteeAmendmentHistoryRecords
            .AsNoTracking()
            .Where(x => x.GuaranteeCaseId == caseId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (GuaranteeAmendmentHistoryStatus?)x.Status)
            .FirstOrDefaultAsync(ct);

    private GuaranteeAmendmentHistoryRecord BuildPendingAmendmentHistoryRecord(GuaranteeCase entity, string authUserId)
        => new(
            entity.Id,
            entity.AmendmentType!.Value,
            SerializeSnapshot(BuildCurrentSourceSnapshot(entity)),
            SerializeSnapshot(BuildRequestedSnapshot(entity)),
            entity.AmendmentReason ?? string.Empty,
            authUserId,
            clock.UtcNow);

    private GuaranteeAmendmentValueSnapshotDto BuildCurrentSourceSnapshot(GuaranteeCase entity)
        => new(
            entity.ApprovalForm?.ExpiryDate ?? entity.Application?.ValidityTo,
            entity.ApprovalForm?.GuaranteeAmount ?? entity.Application?.RequestedGuaranteeAmount);

    private GuaranteeAmendmentValueSnapshotDto BuildRequestedSnapshot(GuaranteeCase entity)
    {
        var current = BuildCurrentSourceSnapshot(entity);
        return entity.AmendmentType!.Value switch
        {
            AmendmentType.Extension => current with { ValidityTo = entity.AmendmentRequestedValidityTo },
            AmendmentType.Reduction => current with { GuaranteeAmount = entity.AmendmentRequestedAmount },
            AmendmentType.Cancellation => current with { GuaranteeAmount = 0m, ValidityTo = current.ValidityTo },
            _ => current
        };
    }

    private GuaranteeAmendmentDto? BuildAmendmentDto(
        GuaranteeCase entity,
        IReadOnlyList<GuaranteeAmendmentHistoryRecordDto> history)
    {
        var baseDto = dtoMapper.MapAmendment(entity);
        if (baseDto is null)
            return null;

        var latest = history.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        return baseDto with
        {
            PreviousValues = latest?.PreviousValues,
            NewValues = latest?.NewValues,
            History = history
        };
    }

    private async Task<IReadOnlyList<GuaranteeAmendmentHistoryRecordDto>> LoadAmendmentHistoryAsync(Guid caseId, CancellationToken ct)
    {
        var records = await dbContext.GuaranteeAmendmentHistoryRecords
            .AsNoTracking()
            .Where(x => x.GuaranteeCaseId == caseId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        if (records.Count == 0)
            return [];

        var userIds = records
            .SelectMany(x => new[] { x.CreatedBy, x.ApprovalUser })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;

        var lookup = userIds.Length == 0
            ? new Dictionary<string, UserDisplayDto>()
            : await userDisplayLookup.GetByIdsAsync(userIds!, ct);

        return records
            .Select(x => new GuaranteeAmendmentHistoryRecordDto(
                x.Id,
                x.AmendmentType,
                x.Status,
                DeserializeSnapshot(x.PreviousValues),
                DeserializeSnapshot(x.NewValues),
                x.Reason,
                x.CreatedBy,
                lookup.GetValueOrDefault(x.CreatedBy)?.FullName,
                x.CreatedAt,
                x.ApprovalUser,
                string.IsNullOrWhiteSpace(x.ApprovalUser) ? null : lookup.GetValueOrDefault(x.ApprovalUser)?.FullName,
                x.ApprovedAt,
                x.DecisionReason))
            .ToList();
    }

    private static string SerializeSnapshot(GuaranteeAmendmentValueSnapshotDto snapshot)
        => JsonSerializer.Serialize(snapshot);

    private static GuaranteeAmendmentValueSnapshotDto? DeserializeSnapshot(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<GuaranteeAmendmentValueSnapshotDto>(json);

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

    private async Task NotifyWorkflowSmsSafeAsync(
        Guid caseId,
        string applicantUserId,
        string caseNumber,
        int fromStatus,
        int toStatus,
        CancellationToken cancellationToken)
    {
        // #region agent log
        AgentDebugLog.Write("H2", "GuaranteeCaseAppService.NotifyWorkflowSmsSafeAsync", "entry", new { caseId, fromStatus, toStatus });
        // #endregion
        try
        {
            await workflowSmsNotifier.NotifyStepChangeAsync(
                new WorkflowSmsNotification(
                    CaseModuleType.Guarantee,
                    caseId,
                    applicantUserId,
                    caseNumber,
                    fromStatus,
                    toStatus),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workflow SMS notification failed for guarantee case {CaseId}", caseId);
        }
        // #region agent log
        AgentDebugLog.Write("H2", "GuaranteeCaseAppService.NotifyWorkflowSmsSafeAsync", "exit", new { caseId });
        // #endregion
    }
}
