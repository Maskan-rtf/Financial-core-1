using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using Core.Application.Abstractions;
using Core.Application.Authorization;
using Core.Application.Common;
using Core.Application.DTOs;
using Core.Application.Kanban;
using Core.Application.Logging;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public interface ICaseCommentsAuditAppService
{
    Task<Result<CaseCommentsAuditListResult>> GetByCaseAsync(
        AuditedCaseType caseType,
        Guid caseId,
        bool includeInternal,
        CancellationToken cancellationToken);

    Task<Result<CaseCommentsAuditListResult>> GetPagedAsync(
        AuditedCaseType? caseType,
        int take,
        int skip,
        bool includeInternal,
        CancellationToken cancellationToken);
}

public sealed class CaseCommentsAuditAppService(
    ICoreDbContext dbContext,
    ICoreUnitOfWork unitOfWork,
    IUserDisplayLookup userDisplayLookup,
    IUserContext userContext,
    ICaseAuthorizationService caseAuthorization,
    ILoanAuthorizationService loanAuthorization,
    IGuaranteeAuthorizationService guaranteeAuthorization,
    ILogger<CaseCommentsAuditAppService> logger) : ICaseCommentsAuditAppService
{
    #region Public API

    public async Task<Result<CaseCommentsAuditListResult>> GetByCaseAsync(
        AuditedCaseType caseType,
        Guid caseId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure)
        {
            ApplicationLog.Blocked(logger, "GetCaseCommentsAuditByCase", "user is not authenticated", caseId: caseId);
            return Result<CaseCommentsAuditListResult>.Fail(authResult.Error!);
        }

        ApplicationLog.Started(logger, "GetCaseCommentsAuditByCase", authResult.Value, caseId);

        if (!await CanAccessCaseAsync(caseType, caseId, authResult.Value!, cancellationToken))
        {
            ApplicationLog.Blocked(logger, "GetCaseCommentsAuditByCase", "case not found or access denied", authResult.Value, caseId);
            return Result<CaseCommentsAuditListResult>.Fail(Error.NotFound(ResolveCaseNotFoundMessage(caseType)));
        }

        var items = caseType switch
        {
            AuditedCaseType.Investment => await LoadInvestmentCommentsAsync(caseId, includeInternal, cancellationToken),
            AuditedCaseType.Loan => await LoadLoanCommentsAsync(caseId, includeInternal, cancellationToken),
            AuditedCaseType.Guarantee => await LoadGuaranteeCommentsAsync(caseId, includeInternal, cancellationToken),
            _ => []
        };

        var ordered = items.OrderBy(x => x.CreatedAt).ToArray();
        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} audit comment(s) for {CaseType} case {CaseId} (includeInternal={IncludeInternal})",
            authResult.Value, ordered.Length, caseType, caseId, includeInternal);

        return Result<CaseCommentsAuditListResult>.Ok(new CaseCommentsAuditListResult(ordered, 0, ordered.Length, ordered.Length));
    }

    public async Task<Result<CaseCommentsAuditListResult>> GetPagedAsync(
        AuditedCaseType? caseType,
        int take,
        int skip,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        var authResult = RequireUserId();
        if (authResult.IsFailure)
        {
            ApplicationLog.Blocked(logger, "GetCaseCommentsAuditPaged", "user is not authenticated");
            return Result<CaseCommentsAuditListResult>.Fail(authResult.Error!);
        }

        ApplicationLog.Started(logger, "GetCaseCommentsAuditPaged", authResult.Value);

        var pageSize = Math.Clamp(take, 1, 200);
        var normalizedSkip = Math.Max(0, skip);

        var items = new List<CaseCommentAuditItemDto>();
        if (caseType is null or AuditedCaseType.Investment)
            items.AddRange(await LoadAllInvestmentCommentsAsync(authResult.Value!, includeInternal, cancellationToken));
        if (caseType is null or AuditedCaseType.Loan)
            items.AddRange(await LoadAllLoanCommentsAsync(authResult.Value!, includeInternal, cancellationToken));
        if (caseType is null or AuditedCaseType.Guarantee)
            items.AddRange(await LoadAllGuaranteeCommentsAsync(authResult.Value!, includeInternal, cancellationToken));

        var ordered = items
            .OrderByDescending(x => x.CreatedAt)
            .ToArray();
        var page = ordered.Skip(normalizedSkip).Take(pageSize).ToArray();

        ApplicationLog.Completed(logger,
            "User {UserId} listed {Count} of {Total} audit comment(s) (caseType={CaseType}, includeInternal={IncludeInternal}, skip={Skip}, take={Take})",
            authResult.Value, page.Length, ordered.Length, caseType?.ToString() ?? "all", includeInternal, normalizedSkip, pageSize);

        return Result<CaseCommentsAuditListResult>.Ok(
            new CaseCommentsAuditListResult(page, normalizedSkip, pageSize, ordered.Length));
    }

    #endregion

    #region Access Control

    private Result<string> RequireUserId()
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result<string>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        return Result<string>.Ok(userContext.UserId);
    }

    private async Task<bool> CanAccessCaseAsync(
        AuditedCaseType caseType,
        Guid caseId,
        string userId,
        CancellationToken cancellationToken)
        => caseType switch
        {
            AuditedCaseType.Investment when CanViewInvestmentModule() =>
                await unitOfWork.InvestmentCases.GetDetailProjectionAsync(
                    caseId, userId, caseAuthorization.IsInternalUser, cancellationToken) is not null,
            AuditedCaseType.Loan when CanViewLoanModule() =>
                await unitOfWork.LoanCases.GetDetailProjectionAsync(
                    caseId, userId, loanAuthorization.IsInternalUser, cancellationToken) is not null,
            AuditedCaseType.Guarantee when CanViewGuaranteeModule() =>
                await unitOfWork.GuaranteeCases.ExistsScopedAsync(
                    caseId, userId, guaranteeAuthorization.IsInternalUser, cancellationToken),
            _ => false
        };

    private bool CanViewInvestmentModule()
        => caseAuthorization.HasPermission(CasePermissions.ReadAll)
           || caseAuthorization.HasPermission(CasePermissions.ReadOwn);

    private bool CanViewLoanModule()
        => loanAuthorization.HasPermission(LoanPermissions.ReadAll)
           || loanAuthorization.HasPermission(LoanPermissions.ReadOwn);

    private bool CanViewGuaranteeModule()
        => guaranteeAuthorization.HasPermission(GuaranteePermissions.ReadAll)
           || guaranteeAuthorization.HasPermission(GuaranteePermissions.ReadOwn);

    private bool CanViewInternalInvestmentComments(bool includeInternal)
        => includeInternal && caseAuthorization.HasPermission(CasePermissions.ViewInternalComments);

    private bool CanViewInternalLoanComments(bool includeInternal)
        => includeInternal && loanAuthorization.HasPermission(LoanPermissions.ViewInternalComments);

    private bool CanViewInternalGuaranteeComments(bool includeInternal)
        => includeInternal && guaranteeAuthorization.HasPermission(GuaranteePermissions.ViewInternalComments);

    private bool CanViewAllInvestmentCases()
        => caseAuthorization.IsInternalUser && caseAuthorization.HasPermission(CasePermissions.ReadAll);

    private bool CanViewAllLoanCases()
        => loanAuthorization.IsInternalUser && loanAuthorization.HasPermission(LoanPermissions.ReadAll);

    private bool CanViewAllGuaranteeCases()
        => guaranteeAuthorization.IsInternalUser && guaranteeAuthorization.HasPermission(GuaranteePermissions.ReadAll);

    #endregion

    #region Data Loading

    private async Task<IReadOnlyList<CaseCommentAuditItemDto>> LoadInvestmentCommentsAsync(
        Guid caseId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        var canViewInternal = CanViewInternalInvestmentComments(includeInternal);
        var rows = await dbContext.CaseComments
            .AsNoTracking()
            .Where(x => x.CaseId == caseId && (canViewInternal || !x.IsInternal))
            .Include(x => x.Attachments)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        var caseMeta = await dbContext.InvestmentCases.AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new { x.CaseNumber, x.Title })
            .FirstOrDefaultAsync(cancellationToken);

        var history = await dbContext.CaseWorkflowHistories
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .ToListAsync(cancellationToken);

        var userLookup = await userDisplayLookup.GetByIdsAsync(rows.Select(x => x.SenderUserId), cancellationToken);

        return rows.Select(comment => MapInvestmentAuditItem(
            comment,
            caseId,
            caseMeta?.CaseNumber,
            caseMeta?.Title,
            userDisplayLookup.ResolveFullName(userLookup, comment.SenderUserId),
            history)).ToArray();
    }

    private async Task<IReadOnlyList<CaseCommentAuditItemDto>> LoadLoanCommentsAsync(
        Guid caseId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        var canViewInternal = CanViewInternalLoanComments(includeInternal);
        var rows = await dbContext.LoanCaseComments
            .AsNoTracking()
            .Where(x => x.CaseId == caseId && (canViewInternal || !x.IsInternal))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        var caseMeta = await dbContext.LoanCases.AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new { x.CaseNumber, x.Title })
            .FirstOrDefaultAsync(cancellationToken);

        var history = await dbContext.LoanCaseWorkflowHistories
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .ToListAsync(cancellationToken);

        var userLookup = await userDisplayLookup.GetByIdsAsync(rows.Select(x => x.SenderUserId), cancellationToken);

        return rows.Select(comment => MapLoanAuditItem(
            comment,
            caseId,
            caseMeta?.CaseNumber,
            caseMeta?.Title,
            userDisplayLookup.ResolveFullName(userLookup, comment.SenderUserId),
            history)).ToArray();
    }

    private async Task<IReadOnlyList<CaseCommentAuditItemDto>> LoadGuaranteeCommentsAsync(
        Guid caseId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        var canViewInternal = CanViewInternalGuaranteeComments(includeInternal);
        var rows = await dbContext.GuaranteeCaseComments
            .AsNoTracking()
            .Where(x => x.CaseId == caseId && (canViewInternal || !x.IsInternal))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        var caseMeta = await dbContext.GuaranteeCases.AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new { x.CaseNumber, x.Title })
            .FirstOrDefaultAsync(cancellationToken);

        var history = await dbContext.GuaranteeCaseWorkflowHistories
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .ToListAsync(cancellationToken);

        var userLookup = await userDisplayLookup.GetByIdsAsync(rows.Select(x => x.SenderUserId), cancellationToken);

        return rows.Select(comment => MapGuaranteeAuditItem(
            comment,
            caseId,
            caseMeta?.CaseNumber,
            caseMeta?.Title,
            userDisplayLookup.ResolveFullName(userLookup, comment.SenderUserId),
            history)).ToArray();
    }

    private async Task<IReadOnlyList<CaseCommentAuditItemDto>> LoadAllInvestmentCommentsAsync(
        string userId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        if (!CanViewInvestmentModule())
            return [];

        var canViewInternal = CanViewInternalInvestmentComments(includeInternal);
        var query = dbContext.CaseComments
            .AsNoTracking()
            .Where(x => canViewInternal || !x.IsInternal);

        if (!CanViewAllInvestmentCases())
            query = query.Where(x => x.Case.ApplicantUserId == userId);

        var rows = await query
            .Include(x => x.Attachments)
            .Include(x => x.Case)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        var caseIds = rows.Select(x => x.CaseId).Distinct().ToArray();
        var histories = await dbContext.CaseWorkflowHistories
            .AsNoTracking()
            .Where(x => caseIds.Contains(x.CaseId))
            .ToListAsync(cancellationToken);
        var historyByCase = histories.GroupBy(x => x.CaseId).ToDictionary(x => x.Key, x => x.ToList());

        var userLookup = await userDisplayLookup.GetByIdsAsync(rows.Select(x => x.SenderUserId), cancellationToken);

        return rows.Select(comment => MapInvestmentAuditItem(
            comment,
            comment.CaseId,
            comment.Case.CaseNumber,
            comment.Case.Title,
            userDisplayLookup.ResolveFullName(userLookup, comment.SenderUserId),
            historyByCase.GetValueOrDefault(comment.CaseId) ?? [])).ToArray();
    }

    private async Task<IReadOnlyList<CaseCommentAuditItemDto>> LoadAllLoanCommentsAsync(
        string userId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        if (!CanViewLoanModule())
            return [];

        var canViewInternal = CanViewInternalLoanComments(includeInternal);
        var query = dbContext.LoanCaseComments
            .AsNoTracking()
            .Where(x => canViewInternal || !x.IsInternal);

        if (!CanViewAllLoanCases())
            query = query.Where(x => x.Case.ApplicantUserId == userId);

        var rows = await query
            .Include(x => x.Case)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        var caseIds = rows.Select(x => x.CaseId).Distinct().ToArray();
        var histories = await dbContext.LoanCaseWorkflowHistories
            .AsNoTracking()
            .Where(x => caseIds.Contains(x.CaseId))
            .ToListAsync(cancellationToken);
        var historyByCase = histories.GroupBy(x => x.CaseId).ToDictionary(x => x.Key, x => x.ToList());

        var userLookup = await userDisplayLookup.GetByIdsAsync(rows.Select(x => x.SenderUserId), cancellationToken);

        return rows.Select(comment => MapLoanAuditItem(
            comment,
            comment.CaseId,
            comment.Case.CaseNumber,
            comment.Case.Title,
            userDisplayLookup.ResolveFullName(userLookup, comment.SenderUserId),
            historyByCase.GetValueOrDefault(comment.CaseId) ?? [])).ToArray();
    }

    private async Task<IReadOnlyList<CaseCommentAuditItemDto>> LoadAllGuaranteeCommentsAsync(
        string userId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        if (!CanViewGuaranteeModule())
            return [];

        var canViewInternal = CanViewInternalGuaranteeComments(includeInternal);
        var query = dbContext.GuaranteeCaseComments
            .AsNoTracking()
            .Where(x => canViewInternal || !x.IsInternal);

        if (!CanViewAllGuaranteeCases())
            query = query.Where(x => x.Case.ApplicantUserId == userId);

        var rows = await query
            .Include(x => x.Case)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        var caseIds = rows.Select(x => x.CaseId).Distinct().ToArray();
        var histories = await dbContext.GuaranteeCaseWorkflowHistories
            .AsNoTracking()
            .Where(x => caseIds.Contains(x.CaseId))
            .ToListAsync(cancellationToken);
        var historyByCase = histories.GroupBy(x => x.CaseId).ToDictionary(x => x.Key, x => x.ToList());

        var userLookup = await userDisplayLookup.GetByIdsAsync(rows.Select(x => x.SenderUserId), cancellationToken);

        return rows.Select(comment => MapGuaranteeAuditItem(
            comment,
            comment.CaseId,
            comment.Case.CaseNumber,
            comment.Case.Title,
            userDisplayLookup.ResolveFullName(userLookup, comment.SenderUserId),
            historyByCase.GetValueOrDefault(comment.CaseId) ?? [])).ToArray();
    }

    #endregion

    #region Mapping

    private static CaseCommentAuditItemDto MapInvestmentAuditItem(
        InvestmentCaseComment comment,
        Guid caseId,
        string? caseNumber,
        string? caseTitle,
        string? senderFullName,
        IReadOnlyList<InvestmentCaseWorkflowHistory> history)
    {
        var snapshots = history
            .Select(h => new WorkflowHistorySnapshot<CaseStatus>(h.CreatedAt, h.FromStatus, h.ToStatus, h.Comment))
            .ToArray();

        var resolved = CaseCommentWorkflowStatusResolver.ResolveFromHistory(
            comment.WorkflowStatusAtCreation.HasValue ? (int)comment.WorkflowStatusAtCreation.Value : null,
            comment.CreatedAt,
            comment.Message,
            comment.IsRevisionRequest,
            snapshots);

        var statusLabel = resolved.HasValue
            ? CaseKanbanRules.GetStatusTitle((CaseStatus)resolved.Value)
            : null;

        return new CaseCommentAuditItemDto(
            comment.Id,
            AuditedCaseType.Investment,
            caseId,
            caseNumber,
            caseTitle,
            (int)comment.Phase,
            ResolveInvestmentPhaseLabel(comment.Phase),
            comment.SenderUserId,
            senderFullName,
            comment.SenderRole,
            comment.Message,
            comment.IsRevisionRequest,
            comment.IsInternal,
            comment.ParentId,
            comment.Attachments
                .Select(a => new CaseCommentAuditAttachmentDto(a.Id, a.S3Key, a.FileName))
                .ToArray(),
            comment.CreatedAt,
            resolved,
            statusLabel);
    }

    private static CaseCommentAuditItemDto MapLoanAuditItem(
        LoanCaseComment comment,
        Guid caseId,
        string? caseNumber,
        string? caseTitle,
        string? senderFullName,
        IReadOnlyList<LoanCaseWorkflowHistory> history)
    {
        var snapshots = history
            .Select(h => new WorkflowHistorySnapshot<LoanCaseStatus>(h.CreatedAt, h.FromStatus, h.ToStatus, h.Comment))
            .ToArray();

        var resolved = CaseCommentWorkflowStatusResolver.ResolveFromHistory(
            comment.WorkflowStatusAtCreation.HasValue ? (int)comment.WorkflowStatusAtCreation.Value : null,
            comment.CreatedAt,
            comment.Message,
            comment.IsRevisionRequest,
            snapshots);

        var statusLabel = resolved.HasValue
            ? LoanKanbanRules.GetStatusTitle((LoanCaseStatus)resolved.Value)
            : null;

        return new CaseCommentAuditItemDto(
            comment.Id,
            AuditedCaseType.Loan,
            caseId,
            caseNumber,
            caseTitle,
            (int)comment.Phase,
            ResolveLoanPhaseLabel(comment.Phase),
            comment.SenderUserId,
            senderFullName,
            comment.SenderRole,
            comment.Message,
            comment.IsRevisionRequest,
            comment.IsInternal,
            comment.ParentId,
            [],
            comment.CreatedAt,
            resolved,
            statusLabel);
    }

    private static CaseCommentAuditItemDto MapGuaranteeAuditItem(
        GuaranteeCaseComment comment,
        Guid caseId,
        string? caseNumber,
        string? caseTitle,
        string? senderFullName,
        IReadOnlyList<GuaranteeCaseWorkflowHistory> history)
    {
        var snapshots = history
            .Select(h => new WorkflowHistorySnapshot<GuaranteeCaseStatus>(h.CreatedAt, h.FromStatus, h.ToStatus, h.Comment))
            .ToArray();

        var resolved = CaseCommentWorkflowStatusResolver.ResolveFromHistory(
            comment.WorkflowStatusAtCreation.HasValue ? (int)comment.WorkflowStatusAtCreation.Value : null,
            comment.CreatedAt,
            comment.Message,
            comment.IsRevisionRequest,
            snapshots);

        var statusLabel = resolved.HasValue
            ? GuaranteeKanbanRules.GetStatusTitle((GuaranteeCaseStatus)resolved.Value)
            : null;

        return new CaseCommentAuditItemDto(
            comment.Id,
            AuditedCaseType.Guarantee,
            caseId,
            caseNumber,
            caseTitle,
            (int)comment.Phase,
            ResolveGuaranteePhaseLabel(comment.Phase),
            comment.SenderUserId,
            senderFullName,
            comment.SenderRole,
            comment.Message,
            comment.IsRevisionRequest,
            comment.IsInternal,
            comment.ParentId,
            [],
            comment.CreatedAt,
            resolved,
            statusLabel);
    }

    private static string ResolveInvestmentPhaseLabel(CasePhase phase)
        => CaseKanbanRules.GetPhaseTitle(phase);

    private static string ResolveLoanPhaseLabel(LoanCasePhase phase) => phase.ToString();

    private static string ResolveGuaranteePhaseLabel(GuaranteeCasePhase phase) => phase.ToString();

    private static string ResolveCaseNotFoundMessage(AuditedCaseType caseType)
        => caseType switch
        {
            AuditedCaseType.Loan => ApiMessages.LoanCaseNotFound,
            AuditedCaseType.Guarantee => ApiMessages.GuaranteeCaseNotFound,
            _ => ApiMessages.CaseNotFound
        };

    #endregion
}
