using Asp.Versioning;
using Core.API.Http;
using Core.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.API.Controllers;

/// <summary>Role-based analytics dashboards backed by pre-aggregated cache snapshots.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController(
    IExecutiveDashboardService executiveDashboardService,
    IDashboardAnalyticsService analyticsService,
    IDashboardAggregationService aggregationService) : ApiControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyDashboard(CancellationToken ct)
    {
        var result = await analyticsService.GetMyDashboardAsync(ct);
        return Respond(result, "Dashboard loaded.");
    }

    [HttpGet("executive")]
    [Authorize(Policy = "Dashboard.Executive")]
    public async Task<IActionResult> GetExecutiveDashboard(CancellationToken ct)
    {
        var result = await analyticsService.GetMyDashboardAsync(ct);
        return Respond(result, "Executive dashboard loaded.");
    }

    [HttpGet("department")]
    [Authorize(Policy = "Dashboard.Department")]
    public async Task<IActionResult> GetDepartmentDashboard([FromQuery] string? departmentKey, CancellationToken ct)
    {
        var result = await analyticsService.GetDepartmentDashboardAsync(departmentKey, ct);
        return Respond(result, "Department dashboard loaded.");
    }

    [HttpGet("applicant")]
    [Authorize]
    public async Task<IActionResult> GetApplicantDashboard(CancellationToken ct)
    {
        var result = await analyticsService.GetApplicantDashboardAsync(ct);
        return Respond(result, "Applicant dashboard loaded.");
    }

    [HttpGet("ceo")]
    [Authorize(Policy = "Dashboard.Ceo")]
    public async Task<IActionResult> GetCeoDashboard(CancellationToken ct)
    {
        var result = await executiveDashboardService.GetCeoDashboardAsync(ct);
        return Respond(result, "CEO dashboard loaded.");
    }

    [HttpGet("board")]
    [Authorize(Policy = "Dashboard.Board")]
    public async Task<IActionResult> GetBoardDashboard(CancellationToken ct)
    {
        var result = await executiveDashboardService.GetBoardDashboardAsync(ct);
        return Respond(result, "Board dashboard loaded.");
    }

    [HttpGet("admin-overview")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAdminOverview(CancellationToken ct)
    {
        var result = await analyticsService.GetAdminOverviewAsync(ct);
        return Respond(result, "Admin dashboard overview loaded.");
    }

    [HttpPost("refresh")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RefreshDashboardCache(CancellationToken ct)
    {
        await aggregationService.AggregateAllAsync(ct);
        return Respond(BuildingBlocks.Application.Results.Result.Ok(), "Dashboard cache refreshed.");
    }
}
