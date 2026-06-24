using BuildingBlocks.Application.Results;
using Core.Application.DTOs;
using Core.Application.Requests;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface ICaseStageRollbackAppService
{
    Task<Result<CaseStageRollbackOptionsDto>> GetOptionsAsync(CaseModuleType module, Guid caseId, CancellationToken cancellationToken);

    Task<Result<CaseStageRollbackResultDto>> RollbackAsync(
        CaseModuleType module,
        Guid caseId,
        RollbackCaseStageRequest request,
        CancellationToken cancellationToken);
}
