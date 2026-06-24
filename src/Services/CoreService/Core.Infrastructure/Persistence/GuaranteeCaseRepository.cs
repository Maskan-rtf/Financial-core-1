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

public sealed class GuaranteeCaseRepository(CoreDbContext dbContext) : IGuaranteeCaseRepository
{
    public Task<GuaranteeCase?> GetAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.GuaranteeCases
            .AsSplitQuery()
            .Include(x => x.ApplicantCompany)
            .Include(x => x.Application)
            .Include(x => x.ApprovalForm)
            .Include(x => x.Documents)
            .Include(x => x.Comments)
            .Include(x => x.WorkflowHistory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<GuaranteeCase?> GetScopedAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken)
        => ApplyScopedFilter(
                dbContext.GuaranteeCases
                    .AsSplitQuery()
                    .Include(x => x.ApplicantCompany)
                    .Include(x => x.Application)
                    .Include(x => x.ApprovalForm)
                    .Include(x => x.Documents)
                    .Include(x => x.Comments)
                    .Include(x => x.WorkflowHistory),
                userId,
                isInternalUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<GuaranteeCase?> GetScopedForTransitionAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(
                dbContext.GuaranteeCases
                    .AsSplitQuery()
                    .Include(x => x.Application)
                    .Include(x => x.ApprovalForm)
                    .Include(x => x.Documents)
                    .Include(x => x.WorkflowHistory),
                userId,
                isInternalUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<GuaranteeCaseDetailProjection?> GetDetailProjectionAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(dbContext.GuaranteeCases.AsNoTracking(), userId, isInternalUser)
            .Where(x => x.Id == id)
            .Select(x => new GuaranteeCaseDetailProjection(
                x.Id,
                x.CaseNumber,
                x.Title,
                x.ApplicantUserId,
                x.ApplicantType,
                x.CurrentPhase,
                x.CurrentStatus,
                x.WorkflowInstanceId,
                x.AmendmentType,
                x.AmendmentReason,
                x.AmendmentRequestedValidityTo,
                x.AmendmentRequestedAmount,
                x.AmendmentApprovedValidityTo,
                x.AmendmentApprovedAmount,
                x.AmendmentCreatedAt,
                x.AmendmentCompletedAt,
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
                x.Application != null ? x.Application.GuaranteeType : null,
                x.Application != null ? x.Application.ContractSubject : null,
                x.Application != null ? x.Application.IsKnowledgeBasedProduct : null,
                x.Application != null ? x.Application.BeneficiaryName : null,
                x.Application != null ? x.Application.BeneficiaryNationalId : null,
                x.Application != null ? x.Application.BeneficiaryCompanyType : null,
                x.Application != null ? x.Application.ApplicantCategory : ApplicantCategory.None,
                x.Application != null ? x.Application.ApplicantCategoryOther : null,
                x.Application != null ? x.Application.ApplicantLegalForm : null,
                x.Application != null ? x.Application.BaseContractNumber : null,
                x.Application != null ? x.Application.BaseContractAmount : null,
                x.Application != null ? x.Application.BaseContractAmountInWords : null,
                x.Application != null ? x.Application.PriceAdjustmentRatePercent : null,
                x.Application != null ? x.Application.ExecutionProvince : null,
                x.Application != null ? x.Application.RequestedGuaranteeAmount : null,
                x.Application != null ? x.Application.InitialValidityDays : null,
                x.Application != null ? x.Application.ValidityFrom : null,
                x.Application != null ? x.Application.ValidityTo : null,
                x.Application != null ? x.Application.CollateralDescription : null,
                x.Application != null ? x.Application.FacilitySubject : null,
                x.ApprovalForm != null ? x.ApprovalForm.CreditLimitWithCheck : null,
                x.ApprovalForm != null ? x.ApprovalForm.FundIssuedGuaranteesTotal : null,
                x.ApprovalForm != null ? x.ApprovalForm.ActiveCommitments : null,
                x.ApprovalForm != null ? x.ApprovalForm.RemainingCredit : null,
                x.ApprovalForm != null ? x.ApprovalForm.GuaranteeType : null,
                x.ApprovalForm != null ? x.ApprovalForm.GuaranteeAmount : null,
                x.ApprovalForm != null ? x.ApprovalForm.GuaranteeAmountInWords : null,
                x.ApprovalForm != null ? x.ApprovalForm.ContractSubject : null,
                x.ApprovalForm != null ? x.ApprovalForm.Beneficiary : null,
                x.ApprovalForm != null ? x.ApprovalForm.IssuanceDate : null,
                x.ApprovalForm != null ? x.ApprovalForm.ExpiryDate : null,
                x.ApprovalForm != null ? x.ApprovalForm.ActiveDurationDays : null,
                x.ApprovalForm != null ? x.ApprovalForm.DepositRatePercent : null,
                x.ApprovalForm != null ? x.ApprovalForm.DepositAmount : null,
                x.ApprovalForm != null ? x.ApprovalForm.AnnualCommissionRatePercent : null,
                x.ApprovalForm != null ? x.ApprovalForm.CommissionAmount : null,
                x.ApprovalForm != null ? x.ApprovalForm.CollateralDescription : null,
                x.ApprovalForm != null ? x.ApprovalForm.GuarantorsDescription : null,
                x.ApprovalForm != null ? x.ApprovalForm.OtherNotes : null))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> GetWorkflowInstanceIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.GuaranteeCases
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.WorkflowInstanceId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<GuaranteeCase?> GetScopedWithDocumentsAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(dbContext.GuaranteeCases.Include(x => x.Documents), userId, isInternalUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<GuaranteeCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken)
        => dbContext.GuaranteeCases.FirstOrDefaultAsync(x => x.CaseNumber == caseNumber, cancellationToken);

    public Task AddAsync(GuaranteeCase guaranteeCase, CancellationToken cancellationToken)
        => dbContext.GuaranteeCases.AddAsync(guaranteeCase, cancellationToken).AsTask();

    public Task<bool> ExistsCaseNumberAsync(string caseNumber, CancellationToken cancellationToken)
        => dbContext.GuaranteeCases.AnyAsync(x => x.CaseNumber == caseNumber, cancellationToken);

    public async Task<PagedResult<GuaranteeCaseListProjection>> GetPagedAsync(
        GetGuaranteeCasesRequest request,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        var createdAtFrom = PersianDateConverter.TryParseRangeStart(request.CreatedAtFrom);
        var createdAtTo = PersianDateConverter.TryParseRangeEnd(request.CreatedAtTo);

        var query = dbContext.GuaranteeCases.AsNoTracking().AsQueryable();

        query = ApplyScopedFilter(query, userId, isInternalUser);

        if (isInternalUser)
            query = query.WhereIf(
                !string.IsNullOrWhiteSpace(request.ApplicantUserId),
                x => x.ApplicantUserId == request.ApplicantUserId!.Trim());

        query = query
            .ApplyFilters(request, createdAtFrom, createdAtTo)
            .ApplySort(request.SortBy, request.SortDirection);

        var projected = query.Select(x => new GuaranteeCaseListProjection(
            x.Id,
            x.CaseNumber,
            x.Title,
            x.ApplicantUserId,
            x.ApplicantType,
            x.CurrentPhase,
            x.CurrentStatus,
            x.WorkflowInstanceId,
            x.AmendmentType,
            x.AmendmentReason,
            x.AmendmentRequestedValidityTo,
            x.AmendmentRequestedAmount,
            x.AmendmentApprovedValidityTo,
            x.AmendmentApprovedAmount,
            x.AmendmentCreatedAt,
            x.AmendmentCompletedAt,
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
            x.Application != null ? x.Application.GuaranteeType : null,
            x.Application != null ? x.Application.ContractSubject : null,
            x.Application != null ? x.Application.IsKnowledgeBasedProduct : null,
            x.Application != null ? x.Application.BeneficiaryName : null,
            x.Application != null ? x.Application.BeneficiaryNationalId : null,
            x.Application != null ? x.Application.BeneficiaryCompanyType : null,
            x.Application != null ? x.Application.ApplicantCategory : ApplicantCategory.None,
            x.Application != null ? x.Application.ApplicantCategoryOther : null,
            x.Application != null ? x.Application.ApplicantLegalForm : null,
            x.Application != null ? x.Application.BaseContractNumber : null,
            x.Application != null ? x.Application.BaseContractAmount : null,
            x.Application != null ? x.Application.BaseContractAmountInWords : null,
            x.Application != null ? x.Application.PriceAdjustmentRatePercent : null,
            x.Application != null ? x.Application.ExecutionProvince : null,
            x.Application != null ? x.Application.RequestedGuaranteeAmount : null,
            x.Application != null ? x.Application.InitialValidityDays : null,
            x.Application != null ? x.Application.ValidityFrom : null,
            x.Application != null ? x.Application.ValidityTo : null,
            x.Application != null ? x.Application.CollateralDescription : null,
            x.Application != null ? x.Application.FacilitySubject : null));

        return await projected.ToPagedResultAsync(
            request.NormalizedSkip,
            request.NormalizedTake,
            cancellationToken);
    }

    public async Task<IReadOnlyList<GuaranteeWorkflowHistoryListProjection>> GetWorkflowHistoryAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        return await (
            from history in dbContext.GuaranteeCaseWorkflowHistories.AsNoTracking()
            join guaranteeCase in dbContext.GuaranteeCases.AsNoTracking() on history.CaseId equals guaranteeCase.Id
            where guaranteeCase.Id == caseId
                  && (isInternalUser || guaranteeCase.ApplicantUserId == userId)
            orderby history.CreatedAt
            select new GuaranteeWorkflowHistoryListProjection(
                history.Id,
                history.FromPhase,
                history.ToPhase,
                history.FromStatus,
                history.ToStatus,
                history.ChangedByUserId,
                history.Action,
                history.ActorRole,
                history.Comment,
                history.CreatedAt,
                dbContext.Users
                    .Where(u => u.Id.ToString() == history.ChangedByUserId)
                    .Select(u => (u.FirstName + " " + u.LastName).Trim())
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GuaranteeCaseCommentListProjection>> GetCommentsAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        return await (
            from comment in dbContext.GuaranteeCaseComments.AsNoTracking()
            join guaranteeCase in dbContext.GuaranteeCases.AsNoTracking() on comment.CaseId equals guaranteeCase.Id
            where guaranteeCase.Id == caseId
                  && (isInternalUser || guaranteeCase.ApplicantUserId == userId)
            orderby comment.CreatedAt
            select new GuaranteeCaseCommentListProjection(
                comment.Id,
                comment.Phase,
                comment.SenderUserId,
                comment.SenderRole,
                comment.Message,
                comment.IsRevisionRequest,
                comment.IsInternal,
                comment.CreatedAt,
                dbContext.Users
                    .Where(u => u.Id.ToString() == comment.SenderUserId)
                    .Select(u => (u.FirstName + " " + u.LastName).Trim())
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GuaranteeKanbanCaseProjection>> ListActiveKanbanProjectionsAsync(
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        var terminalStatuses = new[]
        {
            GuaranteeCaseStatus.Completed,
            GuaranteeCaseStatus.AmendmentApproved,
            GuaranteeCaseStatus.AmendmentCompleted,
            GuaranteeCaseStatus.AmendmentRejected,
            GuaranteeCaseStatus.Rejected,
            GuaranteeCaseStatus.Cancelled,
            GuaranteeCaseStatus.Archived
        };

        var query = dbContext.GuaranteeCases
            .AsNoTracking()
            .Where(x => !terminalStatuses.Contains(x.CurrentStatus));

        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new GuaranteeKanbanCaseProjection(
                x.Id,
                x.CaseNumber,
                x.ApplicantType,
                x.CurrentPhase,
                x.CurrentStatus,
                x.CreatedAt,
                x.UpdatedAt,
                null,
                x.ApplicantCompany != null ? x.ApplicantCompany.Name : null,
                dbContext.Users
                    .Where(u => u.Id.ToString() == x.ApplicantUserId)
                    .Select(u => (u.FirstName + " " + u.LastName).Trim())
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<GuaranteeCase> ApplyScopedFilter(
        IQueryable<GuaranteeCase> query,
        string userId,
        bool isInternalUser)
    {
        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return query;
    }
}
