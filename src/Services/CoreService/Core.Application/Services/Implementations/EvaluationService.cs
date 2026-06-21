using BuildingBlocks.Application.Validation;
using Core.Application.Common;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Contracts.Paging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Services.CoreService.Core.Application.Abstractions;
using Services.CoreService.Core.Application.Contracts.Evaluations;
using Services.CoreService.Core.Domain.Constants;
using Services.CoreService.Core.Domain.Entities;
using Services.CoreService.Core.Domain.Enums;



namespace Services.CoreService.Core.Application.Services.Implementations;

public sealed class EvaluationService : IEvaluationService
{
    private static readonly HashSet<string> AllowedRoles =
    [
        UserRoleClaims.InvestmentExpert,
        UserRoleClaims.InvestmentManager,
        UserRoleClaims.LegalExpert,
        UserRoleClaims.FinancialExpert,
        UserRoleClaims.Admin
    ];

    private readonly ICoreDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CaseEvaluationUpsertRequest> _validator;

    public EvaluationService(ICoreDbContext db, ICurrentUser currentUser, IValidator<CaseEvaluationUpsertRequest> validator)
    {
        _db = db;
        _currentUser = currentUser;
        _validator = validator;
    }

    public async Task<Result> UpsertAsync(Guid caseId, CaseEvaluationUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result.Fail(Error.Validation(validation.ToErrorMessage()));

        if (!_currentUser.Roles.Overlaps(AllowedRoles))
            return Result.Fail(Error.Forbidden());

        var caseExists = await _db.InvestmentCases.AsNoTracking().AnyAsync(x => x.Id == caseId, ct);
        if (!caseExists)
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));

        var evaluation = await _db.CaseEvaluations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.CaseId == caseId && x.Phase == request.Phase && x.ReviewerUserId == _currentUser.UserId, ct);

        if (evaluation is null)
        {
            evaluation = new InvestmentCaseEvaluation(caseId, request.Phase, _currentUser.UserId, _currentUser.Roles.FirstOrDefault() ?? "Unknown", request.Notes);
            await _db.CaseEvaluations.AddAsync(evaluation, ct);
        }

        var items = request.Items.Select(i => new InvestmentCaseEvaluationItem(evaluation.Id, i.Title, i.IsApproved, i.Comment)).ToList();
        evaluation.SetItems(items);

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<PagedResult<CaseEvaluationUpsertRequest>>> ListAsync(Guid caseId, PagedRequest request, CancellationToken ct)
    {
        if (!_currentUser.Roles.Overlaps(AllowedRoles))
            return Result<PagedResult<CaseEvaluationUpsertRequest>>.Fail(Error.Forbidden());

        var query = _db.CaseEvaluations.AsNoTracking().Where(x => x.CaseId == caseId).OrderByDescending(x => x.CreatedAt);
        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip(request.NormalizedSkip)
            .Take(request.NormalizedTake)
            .Include(x => x.Items)
            .ToListAsync(ct);

        var dtos = items.Select(e =>
            new CaseEvaluationUpsertRequest(
                e.Phase,
                e.Notes,
                e.Items.Select(i => new CaseEvaluationItemRequest(i.Title, i.IsApproved, i.Comment)).ToList()))
            .ToList();

        return Result<PagedResult<CaseEvaluationUpsertRequest>>.Ok(new PagedResult<CaseEvaluationUpsertRequest>(dtos, request.NormalizedSkip, request.NormalizedTake, total));
    }
}
