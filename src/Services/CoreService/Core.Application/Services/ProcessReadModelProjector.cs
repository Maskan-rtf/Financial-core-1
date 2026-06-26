using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Core.Application.Services;

public sealed class ProcessReadModelProjector(
    ICoreDbContext dbContext,
    IInvestmentWorkflowActionProvider investmentWorkflowActionProvider,
    IGuaranteeWorkflowActionProvider guaranteeWorkflowActionProvider,
    ILoanWorkflowActionProvider loanWorkflowActionProvider) : IProcessReadModelProjector
{
    public async Task<Result<ProcessSnapshot>> GetSnapshotAsync(
        CaseModuleType module,
        Guid caseId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        return module switch
        {
            CaseModuleType.Investment => await GetInvestmentSnapshotAsync(caseId, actorRole, cancellationToken),
            CaseModuleType.Guarantee => await GetGuaranteeSnapshotAsync(caseId, actorRole, cancellationToken),
            CaseModuleType.Loan => await GetLoanSnapshotAsync(caseId, actorRole, cancellationToken),
            _ => Result<ProcessSnapshot>.Fail(BuildingBlocks.Application.Errors.Error.Validation(ApiMessages.InvalidCaseModule))
        };
    }

    private async Task<Result<ProcessSnapshot>> GetInvestmentSnapshotAsync(
        Guid caseId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new { x.WorkflowInstanceId, x.CurrentStatus, x.CurrentPhase })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return Result<ProcessSnapshot>.Fail(BuildingBlocks.Application.Errors.Error.NotFound(ApiMessages.CaseNotFound));

        var allowedActions = string.IsNullOrWhiteSpace(actorRole)
            ? []
            : (await investmentWorkflowActionProvider.GetAllowedActionsAsync(
                    caseId,
                    row.WorkflowInstanceId,
                    actorRole,
                    cancellationToken))
                .Select(x => x.ToString())
                .ToArray();

        return Result<ProcessSnapshot>.Ok(new ProcessSnapshot(
            CaseModuleType.Investment,
            caseId,
            row.WorkflowInstanceId,
            (int)row.CurrentStatus,
            (int)row.CurrentPhase,
            allowedActions));
    }

    private async Task<Result<ProcessSnapshot>> GetGuaranteeSnapshotAsync(
        Guid caseId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.GuaranteeCases
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new { x.WorkflowInstanceId, x.CurrentStatus, x.CurrentPhase })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return Result<ProcessSnapshot>.Fail(BuildingBlocks.Application.Errors.Error.NotFound(ApiMessages.GuaranteeCaseNotFound));

        var allowedActions = string.IsNullOrWhiteSpace(actorRole)
            ? []
            : (await guaranteeWorkflowActionProvider.GetAllowedActionsAsync(
                    caseId,
                    row.WorkflowInstanceId,
                    actorRole,
                    cancellationToken))
                .Select(x => x.ToString())
                .ToArray();

        return Result<ProcessSnapshot>.Ok(new ProcessSnapshot(
            CaseModuleType.Guarantee,
            caseId,
            row.WorkflowInstanceId,
            (int)row.CurrentStatus,
            (int)row.CurrentPhase,
            allowedActions));
    }

    private async Task<Result<ProcessSnapshot>> GetLoanSnapshotAsync(
        Guid caseId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.LoanCases
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new { x.WorkflowInstanceId, x.CurrentStatus, x.CurrentPhase })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return Result<ProcessSnapshot>.Fail(BuildingBlocks.Application.Errors.Error.NotFound(ApiMessages.LoanCaseNotFound));

        var allowedActions = string.IsNullOrWhiteSpace(actorRole)
            ? []
            : (await loanWorkflowActionProvider.GetAllowedActionsAsync(
                    caseId,
                    row.WorkflowInstanceId,
                    actorRole,
                    cancellationToken))
                .Select(x => x.ToString())
                .ToArray();

        return Result<ProcessSnapshot>.Ok(new ProcessSnapshot(
            CaseModuleType.Loan,
            caseId,
            row.WorkflowInstanceId,
            (int)row.CurrentStatus,
            (int)row.CurrentPhase,
            allowedActions));
    }
}
