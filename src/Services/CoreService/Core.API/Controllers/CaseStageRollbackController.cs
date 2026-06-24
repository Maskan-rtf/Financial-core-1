using Asp.Versioning;
using Core.API.Http;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.Requests;
using Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.API.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/casestages")]
public sealed class CaseStageRollbackController(ICaseStageRollbackAppService service) : ApiControllerBase
{
    [HttpGet("{module}/{caseId:guid}/options")]
    [Authorize(Policy = "CaseStages.Rollback")]
    public async Task<IActionResult> GetOptions(string module, Guid caseId, CancellationToken ct)
    {
        if (!TryParseModule(module, out var moduleType))
            return BadRequest(ApiMessages.InvalidCaseModule);

        var result = await service.GetOptionsAsync(moduleType, caseId, ct);
        return Respond(result, CaseStageRollbackSuccessMessages.OptionsRetrieved);
    }

    [HttpPut("{module}/{caseId:guid}")]
    [Authorize(Policy = "CaseStages.Rollback")]
    public async Task<IActionResult> Rollback(
        string module,
        Guid caseId,
        [FromBody] RollbackCaseStageRequest request,
        CancellationToken ct)
    {
        if (!TryParseModule(module, out var moduleType))
            return BadRequest(ApiMessages.InvalidCaseModule);

        var result = await service.RollbackAsync(moduleType, caseId, request, ct);
        return Respond(result, CaseStageRollbackSuccessMessages.StageRolledBack);
    }

    private static bool TryParseModule(string module, out CaseModuleType moduleType)
    {
        moduleType = default;
        if (string.IsNullOrWhiteSpace(module))
            return false;

        switch (module.Trim().ToLowerInvariant())
        {
            case "investment":
                moduleType = CaseModuleType.Investment;
                return true;
            case "guarantee":
                moduleType = CaseModuleType.Guarantee;
                return true;
            case "loan":
                moduleType = CaseModuleType.Loan;
                return true;
            default:
                return false;
        }
    }
}
