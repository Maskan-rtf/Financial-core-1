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

public sealed class InvestmentCaseRepository(CoreDbContext dbContext) : IInvestmentCaseRepository
{
    public Task<InvestmentCase?> GetAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.InvestmentCases
            .AsSplitQuery()
            .Include(x => x.ApplicantCompany)
            .Include(x => x.ApplicantProfile)
            .Include(x => x.AttractionBasis)
            .Include(x => x.FinancialWorksheet)
            .Include(x => x.Documents)
            .Include(x => x.Comments)
            .Include(x => x.Revisions)
            .Include(x => x.Evaluations).ThenInclude(x => x.Items)
            .Include(x => x.Valuations)
            .Include(x => x.Payments)
            .Include(x => x.WorkflowHistory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<InvestmentCase?> GetScopedAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken)
        => ApplyScopedFilter(
                dbContext.InvestmentCases
                    .AsSplitQuery()
                    .Include(x => x.ApplicantCompany)
                    .Include(x => x.ApplicantProfile)
                    .Include(x => x.AttractionBasis)
                    .Include(x => x.FinancialWorksheet)
                    .Include(x => x.Documents)
                    .Include(x => x.Comments)
                    .Include(x => x.Revisions)
                    .Include(x => x.Evaluations).ThenInclude(x => x.Items)
                    .Include(x => x.Valuations)
                    .Include(x => x.Payments)
                    .Include(x => x.WorkflowHistory),
                userId,
                isInternalUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<InvestmentCase?> GetScopedForTransitionAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(
                dbContext.InvestmentCases
                    .AsSplitQuery()
                    .Include(x => x.ApplicantProfile)
                    .Include(x => x.AttractionBasis)
                    .Include(x => x.FinancialWorksheet)
                    .Include(x => x.Documents)
                    .Include(x => x.Payments)
                    .Include(x => x.WorkflowHistory),
                userId,
                isInternalUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<InvestmentCaseListProjection?> GetDetailProjectionAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
        => ApplyScopedFilter(dbContext.InvestmentCases.AsNoTracking(), userId, isInternalUser)
            .Where(x => x.Id == id)
            .Select(x => new InvestmentCaseListProjection(
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
                x.ApplicantProfile != null ? x.ApplicantProfile.RepresentativeFullName : null,
                x.ApplicantProfile != null ? x.ApplicantProfile.BusinessStage : null,
                x.ApplicantProfile != null ? x.ApplicantProfile.ContactEmail : null,
                x.ApplicantProfile != null ? x.ApplicantProfile.RequestedAmount : null,
                x.AttractionBasis != null ? x.AttractionBasis.InvestmentAttractionBasis : null))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> GetWorkflowInstanceIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.WorkflowInstanceId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<InvestmentCase?> GetScopedWithDocumentsAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken)
        => ApplyScopedFilter(
                dbContext.InvestmentCases
                    .Include(x => x.Documents),
                userId,
                isInternalUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    private static IQueryable<InvestmentCase> ApplyScopedFilter(
        IQueryable<InvestmentCase> query,
        string userId,
        bool isInternalUser)
    {
        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return query;
    }

    public Task<InvestmentCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken)
        => dbContext.InvestmentCases.FirstOrDefaultAsync(x => x.CaseNumber == caseNumber, cancellationToken);

    public Task AddAsync(InvestmentCase investmentCase, CancellationToken cancellationToken)
        => dbContext.InvestmentCases.AddAsync(investmentCase, cancellationToken).AsTask();

    public Task<bool> ExistsCaseNumberAsync(string caseNumber, CancellationToken cancellationToken)
        => dbContext.InvestmentCases.AnyAsync(x => x.CaseNumber == caseNumber, cancellationToken);

    public async Task<PagedResult<InvestmentCaseListProjection>> GetPagedAsync(
        GetInvestmentCasesRequest request,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        var createdAtFrom = PersianDateConverter.TryParseRangeStart(request.CreatedAtFrom);
        var createdAtTo = PersianDateConverter.TryParseRangeEnd(request.CreatedAtTo);

        var query = dbContext.InvestmentCases.AsNoTracking().AsQueryable();

        query = ApplyScopedFilter(query, userId, isInternalUser);

        if (isInternalUser)
            query = query.WhereIf(
                !string.IsNullOrWhiteSpace(request.ApplicantUserId),
                x => x.ApplicantUserId == request.ApplicantUserId!.Trim());

        query = query
            .ApplyFilters(request, createdAtFrom, createdAtTo)
            .ApplySort(request.SortBy, request.SortDirection);

        var projected = query.Select(x => new InvestmentCaseListProjection(
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
            x.ApplicantProfile != null ? x.ApplicantProfile.RepresentativeFullName : null,
            x.ApplicantProfile != null ? x.ApplicantProfile.BusinessStage : null,
            x.ApplicantProfile != null ? x.ApplicantProfile.ContactEmail : null,
            x.ApplicantProfile != null ? x.ApplicantProfile.RequestedAmount : null,
            x.AttractionBasis != null ? x.AttractionBasis.InvestmentAttractionBasis : null));

        return await projected.ToPagedResultAsync(
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            cancellationToken);
    }

    public async Task<IReadOnlyList<KanbanCaseProjection>> ListActiveKanbanProjectionsAsync(
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken)
    {
        var terminalStatuses = new[]
        {
            CaseStatus.Completed,
            CaseStatus.Rejected,
            CaseStatus.Cancelled,
            CaseStatus.Archived
        };

        var query = dbContext.InvestmentCases
            .AsNoTracking()
            .Where(x => !terminalStatuses.Contains(x.CurrentStatus));

        if (!isInternalUser)
            query = query.Where(x => x.ApplicantUserId == userId);

        return await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new KanbanCaseProjection(
                x.Id,
                x.CaseNumber,
                x.ApplicantType,
                x.CurrentPhase,
                x.CurrentStatus,
                x.CreatedAt,
                x.UpdatedAt,
                x.ApplicantProfile != null ? x.ApplicantProfile.RepresentativeFullName : null,
                x.ApplicantCompany != null ? x.ApplicantCompany.Name : null,
                dbContext.Users
                    .Where(u => u.Id.ToString() == x.ApplicantUserId)
                    .Select(u => (u.FirstName + " " + u.LastName).Trim())
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
