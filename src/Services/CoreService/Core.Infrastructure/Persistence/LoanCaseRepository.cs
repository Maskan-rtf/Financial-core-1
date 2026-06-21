using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Persistence.Queries;
using Core.Application.Abstractions;
using Core.Application.Queries;
using Core.Application.Requests;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure.Persistence;

public sealed class LoanCaseRepository(CoreDbContext dbContext) : ILoanCaseRepository
{
    public Task<LoanCase?> GetAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.LoanCases
            .AsSplitQuery()
            .Include(x => x.ApplicantCompany)
            .Include(x => x.Application)
            .Include(x => x.ApprovalDetail)
            .Include(x => x.Documents)
            .Include(x => x.Installments)
            .Include(x => x.Payments)
            .Include(x => x.Comments)
            .Include(x => x.WorkflowHistory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<LoanCase?> GetScopedAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken)
        => ApplyScopedFilter(
                dbContext.LoanCases
                    .AsSplitQuery()
                    .Include(x => x.ApplicantCompany)
                    .Include(x => x.Application)
                    .Include(x => x.ApprovalDetail)
                    .Include(x => x.Documents)
                    .Include(x => x.Installments)
                    .Include(x => x.Payments)
                    .Include(x => x.Comments)
                    .Include(x => x.WorkflowHistory),
                userId,
                isInternalUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<LoanCase?> GetScopedForTransitionAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(
                dbContext.LoanCases
                    .AsSplitQuery()
                    .Include(x => x.Application)
                    .Include(x => x.ApprovalDetail)
                    .Include(x => x.Documents)
                    .Include(x => x.Installments)
                    .Include(x => x.Payments)
                    .Include(x => x.WorkflowHistory),
                userId,
                isInternalUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<LoanCaseListProjection?> GetDetailProjectionAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(dbContext.LoanCases.AsNoTracking(), userId, isInternalUser)
            .Where(x => x.Id == id)
            .Select(x => new LoanCaseListProjection(
                x.Id,
                x.CaseNumber,
                x.Title,
                x.ApplicantUserId,
                x.ApplicantType,
                x.CurrentPhase,
                x.CurrentStatus,
                x.WorkflowInstanceId,
                x.CreatedAt,
                x.UpdatedAt,
                x.CompletedAt,
                x.ApplicantCompany != null ? x.ApplicantCompany.Id : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.Name : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.EconomicCode : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.RegistrationNumber : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.NationalId : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.PhoneNumber : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.Address : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.City : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.Province : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.PostalCode : null,
                dbContext.Users
                    .Where(u => u.Id.ToString() == x.ApplicantUserId)
                    .Select(u => (u.FirstName + " " + u.LastName).Trim())
                    .FirstOrDefault(),
                dbContext.Users
                    .Where(u => u.Id.ToString() == x.ApplicantUserId)
                    .Select(u => u.PhoneNumber)
                    .FirstOrDefault(),
                x.Application != null ? x.Application.RequestedAmount : null,
                x.Application != null ? x.Application.RequestedAmountInWords : null,
                x.Application != null ? x.Application.FacilitySubject : null,
                x.Application != null ? x.Application.OfferedGuarantees : null,
                x.Application != null ? x.Application.ApplicantCategory : ApplicantCategory.None,
                x.Application != null ? x.Application.ApplicantCategoryOther : null,
                x.Application != null ? x.Application.RepresentativePosition : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.DebtToAssetRatio : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.CurrentRatio : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.ProfitabilityRatioPercent : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.CreditLimitWithCheck : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.IsCreditLineActive : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.RemainingCreditAfterGrant : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.FacilityType : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.ContractSubject : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.BrokerageAndRelatedContract : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.ApprovedAmount : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.ApprovedAmountInWords : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.RepaymentMonths : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.GracePeriodMonths : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.AnnualProfitRatePercent : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.DailyPenaltyRatePercent : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.CollateralDescription : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.GuarantorsDescription : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.OtherNotes : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.ExpectedTotalProfit : null,
                x.ApprovalDetail != null ? x.ApprovalDetail.RepaymentCheckAmount : null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<LoanInstallmentListProjection>> GetInstallmentProjectionsAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        return await (
            from installment in dbContext.LoanInstallments.AsNoTracking()
            join loanCase in dbContext.LoanCases.AsNoTracking() on installment.CaseId equals loanCase.Id
            where loanCase.Id == caseId
                  && (isInternalUser || loanCase.ApplicantUserId == userId)
            orderby installment.RowNumber
            select new LoanInstallmentListProjection(
                installment.Id,
                installment.RowNumber,
                installment.InstallmentDate,
                installment.PrincipalAmount,
                installment.ProfitAmount,
                installment.TotalAmount,
                installment.FundShareOfPrincipal,
                installment.FundShareOfProfit,
                installment.FundShareOfTotal,
                installment.IsGracePeriod,
                installment.IsPaid,
                installment.PaidAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoanPaymentListProjection>> GetPaymentProjectionsAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        return await (
            from payment in dbContext.LoanPayments.AsNoTracking()
            join loanCase in dbContext.LoanCases.AsNoTracking() on payment.CaseId equals loanCase.Id
            where loanCase.Id == caseId
                  && (isInternalUser || loanCase.ApplicantUserId == userId)
            orderby payment.StageNumber
            select new LoanPaymentListProjection(
                payment.Id,
                payment.Amount,
                payment.PaymentDate,
                payment.TransactionNumber,
                payment.ReceiptS3Key,
                payment.Notes,
                payment.StageNumber,
                payment.CreatedByUserId,
                payment.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<string?> GetWorkflowInstanceIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.LoanCases
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.WorkflowInstanceId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<LoanCase?> GetScopedWithDocumentsAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(dbContext.LoanCases.Include(x => x.Documents), userId, isInternalUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<LoanCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken)
        => dbContext.LoanCases.FirstOrDefaultAsync(x => x.CaseNumber == caseNumber, cancellationToken);

    public Task AddAsync(LoanCase loanCase, CancellationToken cancellationToken)
        => dbContext.LoanCases.AddAsync(loanCase, cancellationToken).AsTask();

    public Task<bool> ExistsCaseNumberAsync(string caseNumber, CancellationToken cancellationToken)
        => dbContext.LoanCases.AnyAsync(x => x.CaseNumber == caseNumber, cancellationToken);

    public async Task<PagedResult<LoanCaseListProjection>> GetPagedAsync(
        GetLoanCasesRequest request,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        var createdAtFrom = PersianDateConverter.TryParseRangeStart(request.CreatedAtFrom);
        var createdAtTo = PersianDateConverter.TryParseRangeEnd(request.CreatedAtTo);

        var query = dbContext.LoanCases.AsNoTracking().AsQueryable();

        query = ApplyScopedFilter(query, userId, isInternalUser);

        if (isInternalUser)
            query = query.WhereIf(
                !string.IsNullOrWhiteSpace(request.ApplicantUserId),
                x => x.ApplicantUserId == request.ApplicantUserId!.Trim());

        query = query
            .ApplyFilters(request, createdAtFrom, createdAtTo)
            .ApplySort(request.SortBy, request.SortDirection);

        var projected = query.Select(x => new LoanCaseListProjection(
            x.Id,
            x.CaseNumber,
            x.Title,
            x.ApplicantUserId,
            x.ApplicantType,
            x.CurrentPhase,
            x.CurrentStatus,
            x.WorkflowInstanceId,
            x.CreatedAt,
            x.UpdatedAt,
            x.CompletedAt,
            x.ApplicantCompany != null ? x.ApplicantCompany.Id : null,
            x.ApplicantCompany != null ? x.ApplicantCompany.Name : null,
            x.ApplicantCompany != null ? x.ApplicantCompany.EconomicCode : null,
            x.ApplicantCompany != null ? x.ApplicantCompany.RegistrationNumber : null,
            x.ApplicantCompany != null ? x.ApplicantCompany.NationalId : null,
            x.ApplicantCompany != null ? x.ApplicantCompany.PhoneNumber : null,
            x.ApplicantCompany != null ? x.ApplicantCompany.Address : null,
            x.ApplicantCompany != null ? x.ApplicantCompany.City : null,
            x.ApplicantCompany != null ? x.ApplicantCompany.Province : null,
            x.ApplicantCompany != null ? x.ApplicantCompany.PostalCode : null,
            dbContext.Users
                .Where(u => u.Id.ToString() == x.ApplicantUserId)
                .Select(u => (u.FirstName + " " + u.LastName).Trim())
                .FirstOrDefault(),
            dbContext.Users
                .Where(u => u.Id.ToString() == x.ApplicantUserId)
                .Select(u => u.PhoneNumber)
                .FirstOrDefault(),
            x.Application != null ? x.Application.RequestedAmount : null,
            x.Application != null ? x.Application.RequestedAmountInWords : null,
            x.Application != null ? x.Application.FacilitySubject : null,
            x.Application != null ? x.Application.OfferedGuarantees : null,
            x.Application != null ? x.Application.ApplicantCategory : ApplicantCategory.None,
            x.Application != null ? x.Application.ApplicantCategoryOther : null,
            x.Application != null ? x.Application.RepresentativePosition : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.DebtToAssetRatio : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.CurrentRatio : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.ProfitabilityRatioPercent : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.CreditLimitWithCheck : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.IsCreditLineActive : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.RemainingCreditAfterGrant : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.FacilityType : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.ContractSubject : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.BrokerageAndRelatedContract : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.ApprovedAmount : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.ApprovedAmountInWords : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.RepaymentMonths : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.GracePeriodMonths : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.AnnualProfitRatePercent : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.DailyPenaltyRatePercent : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.CollateralDescription : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.GuarantorsDescription : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.OtherNotes : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.ExpectedTotalProfit : null,
            x.ApprovalDetail != null ? x.ApprovalDetail.RepaymentCheckAmount : null));

        return await projected.ToPagedResultAsync(
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            cancellationToken);
    }

    public async Task<IReadOnlyList<LoanWorkflowHistoryListProjection>> GetWorkflowHistoryAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        return await (
            from history in dbContext.LoanCaseWorkflowHistories.AsNoTracking()
            join loanCase in dbContext.LoanCases.AsNoTracking() on history.CaseId equals loanCase.Id
            where loanCase.Id == caseId
                  && (isInternalUser || loanCase.ApplicantUserId == userId)
            orderby history.CreatedAt
            select new LoanWorkflowHistoryListProjection(
                history.Id,
                history.FromPhase,
                history.ToPhase,
                history.FromStatus,
                history.ToStatus,
                history.ChangedByUserId,
                history.Action,
                history.ActorRole,
                history.CorrelationId,
                history.Comment,
                history.CreatedAt,
                dbContext.Users
                    .Where(u => u.Id.ToString() == history.ChangedByUserId)
                    .Select(u => (u.FirstName + " " + u.LastName).Trim())
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoanCaseCommentListProjection>> GetCommentsAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        return await (
            from comment in dbContext.LoanCaseComments.AsNoTracking()
            join loanCase in dbContext.LoanCases.AsNoTracking() on comment.CaseId equals loanCase.Id
            where loanCase.Id == caseId
                  && (isInternalUser || loanCase.ApplicantUserId == userId)
            orderby comment.CreatedAt
            select new LoanCaseCommentListProjection(
                comment.Id,
                comment.Phase,
                comment.SenderUserId,
                comment.SenderRole,
                comment.Message,
                comment.IsRevisionRequest,
                comment.IsInternal,
                comment.ParentId,
                comment.CreatedAt,
                dbContext.Users
                    .Where(u => u.Id.ToString() == comment.SenderUserId)
                    .Select(u => (u.FirstName + " " + u.LastName).Trim())
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoanKanbanCaseProjection>> ListActiveKanbanProjectionsAsync(
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        var terminalStatuses = new[]
        {
            LoanCaseStatus.Completed,
            LoanCaseStatus.CanceledByCeo,
            LoanCaseStatus.Archived
        };

        var query = dbContext.LoanCases
            .AsNoTracking()
            .Where(x => !terminalStatuses.Contains(x.CurrentStatus));

        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new LoanKanbanCaseProjection(
                x.Id,
                x.CaseNumber,
                x.ApplicantType,
                x.CurrentPhase,
                x.CurrentStatus,
                x.CreatedAt,
                x.UpdatedAt,
                x.Application != null ? x.Application.RequestedAmount : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.Name : null,
                dbContext.Users
                    .Where(u => u.Id.ToString() == x.ApplicantUserId)
                    .Select(u => (u.FirstName + " " + u.LastName).Trim())
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<LoanCase> ApplyScopedFilter(
        IQueryable<LoanCase> query,
        string userId,
        bool isInternalUser)
    {
        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return query;
    }
}
