using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Observability.Correlation;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.DTOs;
using Core.Application.Logging;
using Core.Application.Requests;
using Core.Application.Services;
using Core.Domain.Constants;
using Core.Domain.Entities;
using Core.Domain.Entities.Guarantee;
using Core.Domain.Entities.Investment;
using Core.Domain.Entities.Loan;
using Core.Domain.Enums;
using Core.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public sealed class CaseStageRollbackAppService(
    ICoreUnitOfWork unitOfWork,
    ICoreDbContext dbContext,
    IClock clock,
    IUserContext userContext,
    IProcessManager processManager,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CaseStageRollbackAppService> logger) : ICaseStageRollbackAppService
{
    #region Public API

    public async Task<Result<CaseStageRollbackOptionsDto>> GetOptionsAsync(
        CaseModuleType module,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var auth = RequireAuthorizedUser();
        if (auth.IsFailure)
            return Result<CaseStageRollbackOptionsDto>.Fail(auth.Error!);

        var loaded = await LoadCaseAsync(module, caseId, auth.Value!, cancellationToken);
        if (loaded.IsFailure)
            return Result<CaseStageRollbackOptionsDto>.Fail(loaded.Error!);

        var (currentStatus, history, guaranteeAmendmentType) = loaded.Value!;
        if (!CaseStageOrder.TryGetRank(module, currentStatus, out var currentRank))
            return Result<CaseStageRollbackOptionsDto>.Fail(Error.Validation(ApiMessages.InvalidTargetStage));

        var visited = CaseStageRollbackEvaluator.CollectVisitedStatuses(
            module,
            currentStatus,
            history,
            guaranteeAmendmentType);
        var options = CaseStageRollbackEvaluator.BuildOptions(module, currentStatus, visited);

        return Result<CaseStageRollbackOptionsDto>.Ok(new CaseStageRollbackOptionsDto(
            module,
            caseId,
            currentStatus,
            CaseStageOrder.GetTitle(module, currentStatus),
            currentRank,
            options));
    }

    public async Task<Result<CaseStageRollbackResultDto>> RollbackAsync(
        CaseModuleType module,
        Guid caseId,
        RollbackCaseStageRequest request,
        CancellationToken cancellationToken)
    {
        var auth = RequireAuthorizedUser();
        if (auth.IsFailure)
            return Result<CaseStageRollbackResultDto>.Fail(auth.Error!);

        ApplicationLog.Started(logger, "RollbackCaseStage", auth.Value!, caseId);

        var entityResult = await LoadEntityForRollbackAsync(module, caseId, auth.Value!, cancellationToken);
        if (entityResult.IsFailure)
            return Result<CaseStageRollbackResultDto>.Fail(entityResult.Error!);

        var entity = entityResult.Value!;
        var currentStatus = GetCurrentStatus(entity);
        var guaranteeAmendmentType = GetGuaranteeAmendmentType(entity);
        var history = await LoadWorkflowHistoryAsync(module, caseId, entity, cancellationToken);
        var visited = CaseStageRollbackEvaluator.CollectVisitedStatuses(
            module,
            currentStatus,
            history,
            guaranteeAmendmentType);

        var validation = CaseStageRollbackEvaluator.Validate(
            module,
            currentStatus,
            request.TargetStatus,
            visited,
            guaranteeAmendmentType);
        if (validation.IsFailure)
            return Result<CaseStageRollbackResultDto>.Fail(validation.Error!);

        var actorRole = ResolveActorRole();
        var correlationId = ResolveCorrelationGuid(httpContextAccessor.HttpContext);
        var previousStatus = currentStatus;

        var historyCountBefore = ExtractHistory(entity).Count;
        ApplyRollback(entity, request.TargetStatus, auth.Value!, actorRole, correlationId, request.Comment.Trim());

        if (ExtractHistory(entity).Count <= historyCountBefore)
            return Result<CaseStageRollbackResultDto>.Fail(Error.Validation(ApiMessages.InvalidTargetStage));

        var persistResult = await PersistRollbackAsync(module, entity, cancellationToken);
        if (persistResult.IsFailure)
            return Result<CaseStageRollbackResultDto>.Fail(persistResult.Error!);

        try
        {
            await SignalWorkflowAsync(module, caseId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workflow signal failed after stage rollback for case {CaseId}", caseId);
        }

        ApplicationLog.Completed(
            logger,
            "RollbackCaseStage completed — module {Module}, case {CaseId}: {PreviousStatus} -> {TargetStatus}",
            module,
            caseId,
            previousStatus,
            request.TargetStatus);

        return Result<CaseStageRollbackResultDto>.Ok(new CaseStageRollbackResultDto(
            module,
            caseId,
            previousStatus,
            request.TargetStatus,
            CaseStageOrder.GetTitle(module, request.TargetStatus)));
    }

    #endregion

    #region Private

    private Result<string> RequireAuthorizedUser()
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result<string>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        if (!CaseStageRollbackAuthorization.CanRollbackStage(userContext.Roles))
            return Result<string>.Fail(Error.Forbidden(ApiMessages.OnlyAdminOrTechnicalCanRollbackStage));

        return Result<string>.Ok(userContext.UserId);
    }

    private async Task<Result<(int CurrentStatus, IReadOnlyList<(int FromStatus, int ToStatus)> History, AmendmentType? GuaranteeAmendmentType)>> LoadCaseAsync(
        CaseModuleType module,
        Guid caseId,
        string userId,
        CancellationToken cancellationToken)
    {
        var entityResult = await LoadEntityForRollbackAsync(module, caseId, userId, cancellationToken);
        if (entityResult.IsFailure)
            return Result<(int, IReadOnlyList<(int, int)>, AmendmentType?)>.Fail(entityResult.Error!);

        var entity = entityResult.Value!;
        var history = await LoadWorkflowHistoryAsync(module, caseId, entity, cancellationToken);
        return Result<(int, IReadOnlyList<(int, int)>, AmendmentType?)>.Ok((
            GetCurrentStatus(entity),
            history,
            GetGuaranteeAmendmentType(entity)));
    }

    private async Task<Result<object>> LoadEntityForRollbackAsync(
        CaseModuleType module,
        Guid caseId,
        string userId,
        CancellationToken cancellationToken)
    {
        return module switch
        {
            CaseModuleType.Investment => await LoadInvestmentCaseAsync(caseId, userId, cancellationToken),
            CaseModuleType.Guarantee => await LoadGuaranteeCaseAsync(caseId, userId, cancellationToken),
            CaseModuleType.Loan => await LoadLoanCaseAsync(caseId, userId, cancellationToken),
            _ => Result<object>.Fail(Error.Validation(ApiMessages.InvalidCaseModule))
        };
    }

    private async Task<Result<object>> LoadInvestmentCaseAsync(Guid caseId, string userId, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.InvestmentCases.GetScopedForTransitionAsync(caseId, userId, isInternalUser: true, cancellationToken);
        return entity is null
            ? Result<object>.Fail(Error.NotFound(ApiMessages.CaseNotFound))
            : Result<object>.Ok(entity);
    }

    private async Task<Result<object>> LoadGuaranteeCaseAsync(Guid caseId, string userId, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.GuaranteeCases.GetScopedForTransitionAsync(caseId, userId, isInternalUser: true, cancellationToken);
        return entity is null
            ? Result<object>.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound))
            : Result<object>.Ok(entity);
    }

    private async Task<Result<object>> LoadLoanCaseAsync(Guid caseId, string userId, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.LoanCases.GetScopedForTransitionAsync(caseId, userId, isInternalUser: true, cancellationToken);
        return entity is null
            ? Result<object>.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound))
            : Result<object>.Ok(entity);
    }

    private static AmendmentType? GetGuaranteeAmendmentType(object entity) =>
        entity is GuaranteeCase guarantee ? guarantee.AmendmentType : null;

    private async Task<IReadOnlyList<(int FromStatus, int ToStatus)>> LoadWorkflowHistoryAsync(
        CaseModuleType module,
        Guid caseId,
        object entity,
        CancellationToken cancellationToken)
    {
        return module switch
        {
            CaseModuleType.Guarantee => await dbContext.GuaranteeCaseWorkflowHistories
                .AsNoTracking()
                .Where(x => x.CaseId == caseId)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new ValueTuple<int, int>((int)x.FromStatus, (int)x.ToStatus))
                .ToListAsync(cancellationToken),
            _ => ExtractHistory(entity)
        };
    }

    private static int GetCurrentStatus(object entity) =>
        entity switch
        {
            InvestmentCase investment => (int)investment.CurrentStatus,
            GuaranteeCase guarantee => (int)guarantee.CurrentStatus,
            LoanCase loan => (int)loan.CurrentStatus,
            _ => throw new InvalidOperationException(ApiMessages.UnexpectedError)
        };

    private static IReadOnlyList<(int FromStatus, int ToStatus)> ExtractHistory(object entity) =>
        entity switch
        {
            InvestmentCase investment => investment.WorkflowHistory
                .Select(x => ((int)x.FromStatus, (int)x.ToStatus))
                .ToList(),
            GuaranteeCase guarantee => guarantee.WorkflowHistory
                .Select(x => ((int)x.FromStatus, (int)x.ToStatus))
                .ToList(),
            LoanCase loan => loan.WorkflowHistory
                .Select(x => ((int)x.FromStatus, (int)x.ToStatus))
                .ToList(),
            _ => []
        };

    private static void ApplyRollback(
        object entity,
        int targetStatus,
        string userId,
        string actorRole,
        Guid correlationId,
        string comment)
    {
        switch (entity)
        {
            case InvestmentCase investment:
                investment.RollbackTo((CaseStatus)targetStatus, userId, actorRole, correlationId, comment);
                break;
            case GuaranteeCase guarantee:
                guarantee.RollbackTo((GuaranteeCaseStatus)targetStatus, userId, actorRole, correlationId, comment);
                break;
            case LoanCase loan:
                loan.RollbackTo((LoanCaseStatus)targetStatus, userId, actorRole, correlationId, comment);
                break;
            default:
                throw new InvalidOperationException(ApiMessages.UnexpectedError);
        }
    }

    private async Task<Result> PersistRollbackAsync(
        CaseModuleType module,
        object entity,
        CancellationToken cancellationToken)
    {
        if (dbContext is DbContext efContext)
            efContext.ChangeTracker.Clear();

        var rows = module switch
        {
            CaseModuleType.Investment => await PersistInvestmentRollbackAsync((InvestmentCase)entity, cancellationToken),
            CaseModuleType.Guarantee => await PersistGuaranteeRollbackAsync((GuaranteeCase)entity, cancellationToken),
            CaseModuleType.Loan => await PersistLoanRollbackAsync((LoanCase)entity, cancellationToken),
            _ => 0
        };

        if (rows == 0)
        {
            return module switch
            {
                CaseModuleType.Investment => Result.Fail(Error.NotFound(ApiMessages.CaseNotFound)),
                CaseModuleType.Guarantee => Result.Fail(Error.NotFound(ApiMessages.GuaranteeCaseNotFound)),
                CaseModuleType.Loan => Result.Fail(Error.NotFound(ApiMessages.LoanCaseNotFound)),
                _ => Result.Fail(Error.Validation(ApiMessages.InvalidCaseModule))
            };
        }

        await AddWorkflowHistoryAsync(module, entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private Task<int> PersistInvestmentRollbackAsync(InvestmentCase entity, CancellationToken cancellationToken)
        => dbContext.InvestmentCases.ApplyStateAsync(
            entity.Id,
            entity.CurrentStatus,
            entity.CurrentPhase,
            entity.UpdatedAt ?? clock.UtcNow,
            entity.CompletedAt,
            cancellationToken);

    private Task<int> PersistGuaranteeRollbackAsync(GuaranteeCase entity, CancellationToken cancellationToken)
        => dbContext.GuaranteeCases.ApplyStateAndAmendmentAsync(
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
            cancellationToken);

    private Task<int> PersistLoanRollbackAsync(LoanCase entity, CancellationToken cancellationToken)
        => dbContext.LoanCases.ApplyStateAsync(
            entity.Id,
            entity.CurrentStatus,
            entity.CurrentPhase,
            entity.UpdatedAt ?? clock.UtcNow,
            entity.CompletedAt,
            cancellationToken);

    private Task AddWorkflowHistoryAsync(CaseModuleType module, object entity, CancellationToken cancellationToken)
    {
        return module switch
        {
            CaseModuleType.Investment => dbContext.CaseWorkflowHistories.AddAsync(
                CloneHistory(((InvestmentCase)entity).WorkflowHistory[^1]), cancellationToken).AsTask(),
            CaseModuleType.Guarantee => dbContext.GuaranteeCaseWorkflowHistories.AddAsync(
                CloneHistory(((GuaranteeCase)entity).WorkflowHistory[^1]), cancellationToken).AsTask(),
            CaseModuleType.Loan => dbContext.LoanCaseWorkflowHistories.AddAsync(
                CloneHistory(((LoanCase)entity).WorkflowHistory[^1]), cancellationToken).AsTask(),
            _ => Task.CompletedTask
        };
    }

    private static InvestmentCaseWorkflowHistory CloneHistory(InvestmentCaseWorkflowHistory source)
        => new(
            source.CaseId,
            source.FromPhase,
            source.ToPhase,
            source.FromStatus,
            source.ToStatus,
            source.ChangedByUserId,
            source.Action,
            source.ActorRole,
            source.CorrelationId,
            source.Comment);

    private static GuaranteeCaseWorkflowHistory CloneHistory(GuaranteeCaseWorkflowHistory source)
        => new(
            source.CaseId,
            source.FromPhase,
            source.ToPhase,
            source.FromStatus,
            source.ToStatus,
            source.ChangedByUserId,
            source.Action,
            source.ActorRole,
            source.CorrelationId,
            source.Comment);

    private static LoanCaseWorkflowHistory CloneHistory(LoanCaseWorkflowHistory source)
        => new(
            source.CaseId,
            source.FromPhase,
            source.ToPhase,
            source.FromStatus,
            source.ToStatus,
            source.ChangedByUserId,
            source.Action,
            source.ActorRole,
            source.CorrelationId,
            source.Comment);

    private async Task SignalWorkflowAsync(CaseModuleType module, Guid caseId, CancellationToken cancellationToken)
    {
        if (module is not (CaseModuleType.Investment or CaseModuleType.Guarantee or CaseModuleType.Loan))
            return;

        var signal = await processManager.DispatchAsync(
            new ProcessCommand(
                module,
                caseId,
                "StageRollback",
                Signal: WorkflowSignals.StatusChanged),
            cancellationToken);

        if (signal.IsFailure)
        {
            logger.LogWarning(
                "Workflow signal failed after stage rollback for module {Module} case {CaseId}: {Message}",
                module,
                caseId,
                signal.Error?.Message);
        }
    }

    private string ResolveActorRole()
    {
        if (userContext.Roles.Contains(UserRoleClaims.Admin))
            return UserRoleClaims.Admin;
        if (userContext.Roles.Contains(UserRoleClaims.TechnicalManager))
            return UserRoleClaims.TechnicalManager;
        if (userContext.Roles.Contains(UserRoleClaims.TechnicalExpert))
            return UserRoleClaims.TechnicalExpert;
        return userContext.Roles.FirstOrDefault() ?? UserRoleClaims.Admin;
    }

    private static Guid ResolveCorrelationGuid(HttpContext? httpContext)
    {
        var raw = httpContext?.Items[CorrelationContext.ItemKey]?.ToString()
                  ?? httpContext?.Request.Headers[CorrelationContext.HeaderName].ToString()
                  ?? httpContext?.TraceIdentifier;

        return Guid.TryParse(raw, out var parsed) ? parsed : Guid.NewGuid();
    }

    #endregion
}
