using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Persistence.Queries;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.Queries;
using Core.Application.Requests;
using Core.Domain.Entities;
using Core.Domain.Entities.Guarantee;
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
                    .FirstOrDefault(),
                comment.WorkflowStatusAtCreation))
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
                x.AmendmentType,
                x.WorkflowInstanceId,
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

    public Task<bool> ExistsScopedAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(dbContext.GuaranteeCases.AsNoTracking(), userId, isInternalUser)
            .AnyAsync(x => x.Id == caseId, cancellationToken);

    public Task<GuaranteeCaseStatus?> GetCurrentStatusScopedAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(dbContext.GuaranteeCases.AsNoTracking(), userId, isInternalUser)
            .Where(x => x.Id == caseId)
            .Select(x => (GuaranteeCaseStatus?)x.CurrentStatus)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<GuaranteeCase?> GetAsNoTrackingAsync(Guid caseId, CancellationToken cancellationToken)
        => dbContext.GuaranteeCases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken);

    public Task<int> TouchUpdatedAtAsync(Guid caseId, DateTimeOffset updatedAt, CancellationToken cancellationToken)
        => dbContext.GuaranteeCases.TouchUpdatedAtAsync(caseId, updatedAt, cancellationToken);

    public Task<int> SetTitleAsync(
        Guid caseId,
        string? title,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
        => dbContext.GuaranteeCases.SetTitleAsync(caseId, title, updatedAt, cancellationToken);

    public Task<int> ApplyStateAndAmendmentAsync(
        Guid caseId,
        GuaranteeCaseStatus status,
        GuaranteeCasePhase phase,
        DateTimeOffset updatedAt,
        DateTimeOffset? completedAt,
        AmendmentType? amendmentType,
        string? amendmentReason,
        bool amendmentRequiresCreditReview,
        string? amendmentOriginalGuaranteeReference,
        bool legalOverrideApproved,
        bool settlementConfirmationRequired,
        DateOnly? amendmentRequestedValidityTo,
        decimal? amendmentRequestedAmount,
        DateOnly? amendmentApprovedValidityTo,
        decimal? amendmentApprovedAmount,
        DateTimeOffset? amendmentCreatedAt,
        DateTimeOffset? amendmentCompletedAt,
        CancellationToken cancellationToken)
        => dbContext.GuaranteeCases.ApplyStateAndAmendmentAsync(
            caseId,
            status,
            phase,
            updatedAt,
            completedAt,
            amendmentType,
            amendmentReason,
            amendmentRequiresCreditReview,
            amendmentOriginalGuaranteeReference,
            legalOverrideApproved,
            settlementConfirmationRequired,
            amendmentRequestedValidityTo,
            amendmentRequestedAmount,
            amendmentApprovedValidityTo,
            amendmentApprovedAmount,
            amendmentCreatedAt,
            amendmentCompletedAt,
            cancellationToken);

    public Task AddDocumentAsync(GuaranteeCaseDocument document, CancellationToken cancellationToken)
        => dbContext.GuaranteeCaseDocuments.AddAsync(document, cancellationToken).AsTask();

    public void AddAmendmentHistoryRecord(GuaranteeAmendmentHistoryRecord record)
        => dbContext.GuaranteeAmendmentHistoryRecords.Add(record);

    public Task AddAmendmentHistoryRecordAsync(
        GuaranteeAmendmentHistoryRecord record,
        CancellationToken cancellationToken)
        => dbContext.GuaranteeAmendmentHistoryRecords.AddAsync(record, cancellationToken).AsTask();

    public Task AddWorkflowHistoryAsync(GuaranteeCaseWorkflowHistory history, CancellationToken cancellationToken)
        => dbContext.GuaranteeCaseWorkflowHistories.AddAsync(history, cancellationToken).AsTask();

    public Task InsertWorkflowHistoryAsync(GuaranteeCaseWorkflowHistory history, CancellationToken cancellationToken)
        => dbContext.InsertWorkflowHistoryAsync(history, cancellationToken);

    public Task AddCommentAsync(GuaranteeCaseComment comment, CancellationToken cancellationToken)
        => dbContext.GuaranteeCaseComments.AddAsync(comment, cancellationToken).AsTask();

    public Task InsertCommentAsync(GuaranteeCaseComment comment, CancellationToken cancellationToken)
        => dbContext.InsertCommentAsync(comment, cancellationToken);

    public Task InsertAmendmentHistoryRecordAsync(
        GuaranteeAmendmentHistoryRecord record,
        CancellationToken cancellationToken)
        => dbContext.InsertAmendmentHistoryRecordAsync(record, cancellationToken);

    public Task<int> ApplyLatestPendingAmendmentAuditDecisionAsync(
        Guid caseId,
        GuaranteeAmendmentHistoryStatus status,
        string approvalUser,
        DateTimeOffset decidedAt,
        string? decisionReason,
        CancellationToken cancellationToken)
        => dbContext.GuaranteeAmendmentHistoryRecords.ApplyLatestPendingAmendmentAuditDecisionAsync(
            caseId,
            status,
            approvalUser,
            decidedAt,
            decisionReason,
            cancellationToken);

    public Task<GuaranteeAmendmentHistoryStatus?> GetLatestAmendmentHistoryStatusAsync(
        Guid caseId,
        CancellationToken cancellationToken)
        => dbContext.GuaranteeAmendmentHistoryRecords
            .AsNoTracking()
            .Where(x => x.GuaranteeCaseId == caseId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (GuaranteeAmendmentHistoryStatus?)x.Status)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<GuaranteeAmendmentHistoryRecord>> GetAmendmentHistoryRecordsAsync(
        Guid caseId,
        CancellationToken cancellationToken)
        => await dbContext.GuaranteeAmendmentHistoryRecords
            .AsNoTracking()
            .Where(x => x.GuaranteeCaseId == caseId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public void ClearChangeTracker()
        => dbContext.ChangeTracker.Clear();

    public void DetachTrackedExceptAdded()
    {
        foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
        {
            if (entry.State != EntityState.Added)
                entry.State = EntityState.Detached;
        }
    }

    public IReadOnlyList<string> DescribeTrackedEntries()
        => dbContext.ChangeTracker.Entries()
            .Select(e => $"{e.Entity.GetType().Name}:{e.State}")
            .ToList();

    public IReadOnlyList<GuaranteeAmendmentHistoryRecord> CapturePendingNewAmendmentHistory()
        => dbContext.ChangeTracker
            .Entries<GuaranteeAmendmentHistoryRecord>()
            .Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity)
            .ToList();

    public Task<GuaranteeApprovalForm?> GetApprovalFormAsync(Guid caseId, CancellationToken cancellationToken)
        => dbContext.GuaranteeApprovalForms.FirstOrDefaultAsync(x => x.CaseId == caseId, cancellationToken);

    public Task<bool> ApprovalFormExistsAsync(Guid caseId, CancellationToken cancellationToken)
        => dbContext.GuaranteeApprovalForms.AsNoTracking().AnyAsync(x => x.CaseId == caseId, cancellationToken);

    public Task AddApprovalFormAsync(GuaranteeApprovalForm approvalForm, CancellationToken cancellationToken)
        => dbContext.GuaranteeApprovalForms.AddAsync(approvalForm, cancellationToken).AsTask();

    public Task PersistApprovedAmendmentExtensionAsync(
        Guid caseId,
        DateOnly approvedValidityTo,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        return PersistApprovedAmendmentExtensionInternalAsync(caseId, approvedValidityTo, updatedAt, cancellationToken);
    }

    private async Task PersistApprovedAmendmentExtensionInternalAsync(
        Guid caseId,
        DateOnly approvedValidityTo,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await dbContext.GuaranteeCaseApplications
            .Where(x => x.CaseId == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.ValidityTo, approvedValidityTo)
                    .SetProperty(x => x.UpdatedAt, updatedAt),
                cancellationToken);

        await dbContext.GuaranteeApprovalForms
            .Where(x => x.CaseId == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.ExpiryDate, approvedValidityTo)
                    .SetProperty(
                        x => x.ActiveDurationDays,
                        x => x.IssuanceDate.HasValue
                            ? approvedValidityTo.DayNumber - x.IssuanceDate.Value.DayNumber + 1
                            : x.ActiveDurationDays)
                    .SetProperty(x => x.UpdatedAt, updatedAt),
                cancellationToken);
    }

    public async Task PersistApprovedAmendmentReductionAsync(
        Guid caseId,
        decimal approvedAmount,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await dbContext.GuaranteeCaseApplications
            .Where(x => x.CaseId == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RequestedGuaranteeAmount, approvedAmount)
                    .SetProperty(x => x.UpdatedAt, updatedAt),
                cancellationToken);

        await dbContext.GuaranteeApprovalForms
            .Where(x => x.CaseId == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.GuaranteeAmount, approvedAmount)
                    .SetProperty(x => x.UpdatedAt, updatedAt),
                cancellationToken);
    }

    public async Task PersistApprovedAmendmentCancellationAsync(
        Guid caseId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await dbContext.GuaranteeApprovalForms
            .Where(x => x.CaseId == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.ActiveCommitments, 0m)
                    .SetProperty(x => x.GuaranteeAmount, 0m)
                    .SetProperty(x => x.UpdatedAt, updatedAt),
                cancellationToken);

        await dbContext.GuaranteeCaseApplications
            .Where(x => x.CaseId == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RequestedGuaranteeAmount, 0m)
                    .SetProperty(x => x.UpdatedAt, updatedAt),
                cancellationToken);
    }

    public Task AddApplicantCreditProfileAsync(
        GuaranteeApplicantCreditProfile profile,
        CancellationToken cancellationToken)
        => dbContext.GuaranteeApplicantCreditProfiles.AddAsync(profile, cancellationToken).AsTask();

    public Task<GuaranteeApplicantCreditProfile?> FindApplicantCreditProfileByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken)
        => dbContext.GuaranteeApplicantCreditProfiles
            .FirstOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);

    public Task<GuaranteeApplicantCreditProfile?> FindApplicantCreditProfileByUserAsync(
        string applicantUserId,
        CancellationToken cancellationToken)
        => dbContext.GuaranteeApplicantCreditProfiles
            .FirstOrDefaultAsync(
                x => x.ApplicantUserId == applicantUserId && x.CompanyId == null,
                cancellationToken);

    public async Task<IReadOnlyList<GuaranteeCaseCreditProjection>> GetCreditProjectionsAsync(
        CancellationToken cancellationToken)
        => await dbContext.GuaranteeCases
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .Select(c => new GuaranteeCaseCreditProjection(
                c.CurrentStatus,
                c.CreatedAt,
                c.CompletedAt,
                c.ApprovalForm != null
                    ? c.ApprovalForm.GuaranteeAmount
                    : c.Application != null
                        ? c.Application.RequestedGuaranteeAmount
                        : null,
                c.ApprovalForm != null ? c.ApprovalForm.IssuanceDate : null))
            .ToListAsync(cancellationToken);

    public Task<GuaranteeCaseApplication?> GetApplicationByCaseIdAsync(
        Guid caseId,
        CancellationToken cancellationToken)
        => dbContext.GuaranteeCaseApplications.FirstOrDefaultAsync(x => x.CaseId == caseId, cancellationToken);

    public async Task<GuaranteeCaseApplication> UpsertApplicationAsync(
        Guid caseId,
        UpdateGuaranteeApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var application = await dbContext.GuaranteeCaseApplications
            .FirstOrDefaultAsync(x => x.CaseId == caseId, cancellationToken);

        if (application is null)
        {
            application = new GuaranteeCaseApplication(caseId);
            await dbContext.GuaranteeCaseApplications.AddAsync(application, cancellationToken);
        }

        application.Update(
            request.GuaranteeType,
            request.ContractSubject,
            request.IsKnowledgeBasedProduct,
            request.BeneficiaryName,
            request.BeneficiaryNationalId,
            request.BeneficiaryCompanyType,
            request.ApplicantCategory,
            request.ApplicantCategoryOther,
            request.ApplicantLegalForm,
            request.BaseContractNumber,
            request.BaseContractAmount,
            request.BaseContractAmountInWords,
            request.PriceAdjustmentRatePercent,
            request.ExecutionProvince,
            request.RequestedGuaranteeAmount,
            request.InitialValidityDays,
            request.ValidityFrom,
            request.ValidityTo,
            request.CollateralDescription,
            request.FacilitySubject);

        return application;
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
