using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using Core.Application.Common;
using Core.Application.Logging;
using Microsoft.Extensions.Logging;

namespace Core.Application.Dashboard;

public sealed class ExecutiveDashboardService(
    IDashboardAnalyticsService analyticsService,
    IUserContext userContext,
    ILogger<ExecutiveDashboardService> logger) : IExecutiveDashboardService
{
    #region Public API

    public async Task<Result<CeoDashboardDto>> GetCeoDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
        {
            ApplicationLog.Blocked(logger, "GetCeoDashboard", "user is not authenticated");
            return Result<CeoDashboardDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));
        }

        ApplicationLog.Started(logger, "GetCeoDashboard", userContext.UserId);
        return await analyticsService.GetCeoDashboardAsync(cancellationToken);
    }

    public async Task<Result<BoardDashboardDto>> GetBoardDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
        {
            ApplicationLog.Blocked(logger, "GetBoardDashboard", "user is not authenticated");
            return Result<BoardDashboardDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));
        }

        ApplicationLog.Started(logger, "GetBoardDashboard", userContext.UserId);
        return await analyticsService.GetBoardDashboardAsync(cancellationToken);
    }

    #endregion
}
