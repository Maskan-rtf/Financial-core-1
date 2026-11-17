using System.Net;
using Asp.Versioning;
using Core.API.Http;
using Core.Application.Common;
using Core.Application.Requests;
using Core.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.API.Controllers;

/// <summary>Applicant company profiles linked to cases.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/identity/companies")]
[Authorize]
public sealed class CompaniesController(ICompanyAppService service) : ApiControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => Respond(await service.GetMyCompaniesAsync(ct), CompanySuccessMessages.CompaniesRetrieved);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int take = 50, [FromQuery] int skip = 0, CancellationToken ct = default)
        => Respond(await service.GetAllCompaniesAsync(take, skip, ct), CompanySuccessMessages.CompaniesRetrieved);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCompanyRequest request, CancellationToken ct)
        => Respond(await service.CreateAsync(request, ct), CompanySuccessMessages.CompanyCreated, HttpStatusCode.Created);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCompanyRequest request, CancellationToken ct)
        => Respond(await service.UpdateAsync(id, request, ct), CompanySuccessMessages.CompanyUpdated);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Companies.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => Respond(await service.DeleteAsync(id, ct), CompanySuccessMessages.CompanyDeleted);

    [HttpPost("{id:guid}/logo/presign")]
    public async Task<IActionResult> PresignLogo(Guid id, [FromBody] PresignCompanyLogoRequest request, CancellationToken ct)
        => Respond(await service.PresignLogoUploadAsync(id, request, ct), CompanySuccessMessages.CompanyUpdated);

    [HttpPost("{id:guid}/logo/confirm")]
    public async Task<IActionResult> ConfirmLogo(Guid id, [FromBody] ConfirmCompanyLogoRequest request, CancellationToken ct)
        => Respond(await service.ConfirmLogoUploadAsync(id, request, ct), CompanySuccessMessages.CompanyUpdated);

    [HttpGet("{id:guid}/logo/download")]
    public async Task<IActionResult> DownloadLogo(Guid id, CancellationToken ct)
        => Respond(await service.PresignLogoDownloadAsync(id, ct), CompanySuccessMessages.CompaniesRetrieved);

    [HttpGet("{id:guid}/logo/file")]
    public async Task<IActionResult> DownloadLogoFile(Guid id, CancellationToken ct)
    {
        var result = await service.DownloadLogoFileAsync(id, ct);
        if (result.IsFailure)
            return Respond(result, CompanySuccessMessages.CompaniesRetrieved);

        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
    }
}
