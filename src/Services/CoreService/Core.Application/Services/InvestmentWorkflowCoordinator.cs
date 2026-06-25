using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Observability.Correlation;
using Core.Application.Abstractions;
using Core.Application.Authorization;
using Core.Application.Common;
using Core.Application.Logging;
using Core.Application.Notifications.Sms;
using Core.Domain.Constants;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public sealed class InvestmentWorkflowCoordinator(
    ICoreUnitOfWork unitOfWork,
    ICoreDbContext dbContext,
    ICaseStateManager stateManager,
    ICaseWorkflowOrchestrator workflowOrchestrator,
    IClock clock,
    ICaseAuthorizationService authorizationService,
    ICaseWorkflowSmsNotifier workflowSmsNotifier,
    IHttpContextAccessor httpContextAccessor,
    ILogger<InvestmentWorkflowCoordinator> logger) : IInvestmentWorkflowCoordinator
{
    #region Public API

    public async Task<Result> ApplyTransitionAsync(
        InvestmentWorkflowTransitionRequest request,
        CancellationToken cancellationToken)
    {
        ApplicationLog.Started(logger, $"Workflow:{request.Action}", request.ActorId, request.CaseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedForTransitionAsync(
            request.CaseId, request.ActorId, request.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, $"Workflow:{request.Action}", "case not found or access denied", request.ActorId, request.CaseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        var statusBefore = entity.CurrentStatus;
        var phaseBefore = entity.CurrentPhase;
        var historyCountBefore = entity.WorkflowHistory.Count;
        var commentsCountBefore = entity.Comments.Count;

        var correlationId = ResolveCorrelationGuid(httpContextAccessor.HttpContext);
        var transition = await stateManager.TransitionAsync(
            entity,
            request.Action,
            request.ActorId,
            request.ActorRole,
            request.Comment,
            correlationId);

        if (transition.IsFailure)
        {
            ApplicationLog.Blocked(logger, $"Workflow:{request.Action}",
                transition.Error?.Message ?? "transition rejected by state machine",
                request.ActorId, request.CaseId);
            return transition;
        }

        if (!string.IsNullOrWhiteSpace(request.InternalComment) &&
            SupportsInternalApproveComment(request.Action, statusBefore))
        {
            if (!authorizationService.HasPermission(CasePermissions.CreateInternalComment))
            {
                ApplicationLog.Blocked(logger, $"Workflow:{request.Action}", "cannot create internal comment", request.ActorId, request.CaseId);
                return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
            }

            entity.AddDiscussionComment(
                phaseBefore,
                request.ActorId,
                request.ActorRole,
                request.InternalComment,
                isRevisionRequest: false,
                isInternal: true);
        }

        if (entity.WorkflowHistory.Count > historyCountBefore)
        {
            var persistResult = await PersistCaseTransitionAsync(entity, commentsCountBefore, cancellationToken);
            if (persistResult.IsFailure)
                return persistResult;

            await NotifyWorkflowSmsSafeAsync(
                entity.Id,
                entity.ApplicantUserId,
                entity.CaseNumber,
                statusBefore,
                entity.CurrentStatus,
                request.Action,
                cancellationToken);
        }

        try
        {
            await workflowOrchestrator.SignalAsync(
                request.CaseId,
                WorkflowSignals.StatusChanged,
                payload: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Elsa workflow signal failed for case {CaseId}; domain transition is already saved.",
                request.CaseId);
        }

        ApplicationLog.Completed(logger,
            "User {UserId} (role {Role}) applied {Action} on case {CaseId} ({CaseNumber}): {PhaseBefore}/{StatusBefore} → {PhaseAfter}/{StatusAfter}",
            request.ActorId, request.ActorRole, request.Action, request.CaseId, entity.CaseNumber,
            phaseBefore, statusBefore, entity.CurrentPhase, entity.CurrentStatus);

        return Result.Ok();
    }

    #endregion

    #region Private

    private async Task NotifyWorkflowSmsSafeAsync(
        Guid caseId,
        string applicantUserId,
        string caseNumber,
        CaseStatus from,
        CaseStatus to,
        WorkflowAction action,
        CancellationToken cancellationToken)
    {
        try
        {
            await workflowSmsNotifier.NotifyStatusChangeAsync(
                caseId, applicantUserId, caseNumber, from, to, action, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workflow SMS notification failed for case {CaseId}", caseId);
        }
    }

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

    #endregion
}
