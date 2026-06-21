using BuildingBlocks.Application.Validation;
using Core.Application.Common;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Contracts.Paging;
using FluentValidation;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Services.CoreService.Core.Application.Abstractions;
using Services.CoreService.Core.Application.Contracts.Cases;
using Services.CoreService.Core.Domain.Entities;
using Services.CoreService.Core.Domain.Constants;
using Services.CoreService.Core.Domain.Enums;



namespace Services.CoreService.Core.Application.Services.Implementations;

public sealed class CaseService : ICaseService
{
    private readonly ICoreDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICaseNumberGenerator _caseNumberGenerator;
    private readonly IValidator<CreateCaseRequest> _createValidator;
    private readonly IValidator<SubmitCaseRequest> _submitValidator;
    private readonly ICaseWorkflowOrchestrator _workflowOrchestrator;

    public CaseService(
        ICoreDbContext db,
        ICurrentUser currentUser,
        ICaseNumberGenerator caseNumberGenerator,
        IValidator<CreateCaseRequest> createValidator,
        IValidator<SubmitCaseRequest> submitValidator,
        ICaseWorkflowOrchestrator workflowOrchestrator)
    {
        _db = db;
        _currentUser = currentUser;
        _caseNumberGenerator = caseNumberGenerator;
        _createValidator = createValidator;
        _submitValidator = submitValidator;
        _workflowOrchestrator = workflowOrchestrator;
    }

    public async Task<Result<CaseDto>> CreateAsync(CreateCaseRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<CaseDto>.Fail(Error.Validation(validation.ToErrorMessage()));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var caseNumber = _caseNumberGenerator.NewCaseNumber();
            var entity = new InvestmentCase(caseNumber, _currentUser.UserId, request.ApplicantType);
            await _db.InvestmentCases.AddAsync(entity, ct);

            try
            {
                var workflowInstanceId = await _workflowOrchestrator.StartAsync(entity.Id, ct);
                entity.AttachWorkflowInstance(workflowInstanceId);

                await _db.SaveChangesAsync(ct);
                return Result<CaseDto>.Ok(entity.Adapt<CaseDto>());
            }
            catch (DbUpdateException)
            {
                // Unique constraint on CaseNumber could collide; retry.
            }
        }

        return Result<CaseDto>.Fail(Error.Unexpected(ApiMessages.CaseNumberAllocationFailed));
    }

    public async Task<Result<CaseDto>> GetAsync(Guid caseId, CancellationToken ct)
    {
        var entity = await _db.InvestmentCases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == caseId, ct);
        if (entity is null)
            return Result<CaseDto>.Fail(Error.NotFound(ApiMessages.CaseNotFound));

        if (entity.ApplicantUserId != _currentUser.UserId && !_currentUser.Roles.Contains(UserRoleClaims.Admin))
            return Result<CaseDto>.Fail(Error.Forbidden());

        return Result<CaseDto>.Ok(entity.Adapt<CaseDto>());
    }

    public async Task<Result<PagedResult<CaseDto>>> ListMyCasesAsync(PagedRequest request, CancellationToken ct)
    {
        var query = _db.InvestmentCases.AsNoTracking()
            .Where(x => x.ApplicantUserId == _currentUser.UserId);

        query = request.Sort switch
        {
            "createdAt" => query.OrderBy(x => x.CreatedAt),
            "-createdAt" => query.OrderByDescending(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip(request.NormalizedSkip)
            .Take(request.NormalizedTake)
            .ProjectToType<CaseDto>()
            .ToListAsync(ct);

        return Result<PagedResult<CaseDto>>.Ok(new PagedResult<CaseDto>(items, request.NormalizedSkip, request.NormalizedTake, total));
    }

    public async Task<Result> SubmitAsync(Guid caseId, SubmitCaseRequest request, CancellationToken ct)
    {
        var validation = await _submitValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result.Fail(Error.Validation(validation.ToErrorMessage()));

        var entity = await _db.InvestmentCases
            .Include(x => x.WorkflowHistory)
            .FirstOrDefaultAsync(x => x.Id == caseId, ct);

        if (entity is null)
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));

        if (entity.ApplicantUserId != _currentUser.UserId)
            return Result.Fail(Error.Forbidden());

        entity.Submit(_currentUser.UserId, request.Comment);

        await _workflowOrchestrator.SignalAsync(entity.Id, signal: WorkflowSignals.StatusChanged, payload: new { entity.Id }, ct);
        await _db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
