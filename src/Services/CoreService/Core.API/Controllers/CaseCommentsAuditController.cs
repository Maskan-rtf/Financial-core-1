using Asp.Versioning;
using Core.API.Http;
using Core.Application.Common;
using Core.Application.DTOs;
using Core.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.API.Controllers;

/// <summary>Read-only audit views of case comments across investment, loan, and guarantee workflows.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/audit/case-comments")]
[Authorize]
public sealed class CaseCommentsAuditController(ICaseCommentsAuditAppService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] AuditedCaseType? caseType = null,
        [FromQuery] bool includeInternal = true,
        [FromQuery] int take = 50,
        [FromQuery] int skip = 0,
        CancellationToken ct = default)
    {
        var result = await service.GetPagedAsync(caseType, take, skip, includeInternal, ct);
        return Respond(result, AuditSuccessMessages.CaseCommentsRetrieved);
    }

    [HttpGet("{caseType}/{caseId:guid}")]
    public async Task<IActionResult> GetByCase(
        AuditedCaseType caseType,
        Guid caseId,
        [FromQuery] bool includeInternal = true,
        CancellationToken ct = default)
    {
        var result = await service.GetByCaseAsync(caseType, caseId, includeInternal, ct);
        return Respond(result, AuditSuccessMessages.CaseCommentsRetrieved);
    }
}
