using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Observability.Correlation;
using Core.Application.Abstractions;
using Core.Application.Authorization;
using Core.Application.Common;
using Core.Application.Logging;
using Core.Domain.Constants;
using Core.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public sealed class InvestmentWorkflowCoordinator(
    ICoreUnitOfWork unitOfWork,
    IProcessManager processManager,
    ICaseAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<InvestmentWorkflowCoordinator> logger) : IInvestmentWorkflowCoordinator
{
    public async Task<Result> ApplyTransitionAsync(
        InvestmentWorkflowTransitionRequest request,
        CancellationToken cancellationToken)
    {
        ApplicationLog.Started(logger, $"Workflow:{request.Action}", request.ActorId, request.CaseId);

        var entity = await unitOfWork.InvestmentCases.GetScopedForTransitionAsync(
            request.CaseId, request.ActorId, request.IsInternalUser, cancellationToken);
        if (entity is null)
        {
            ApplicationLog.Blocked(logger, $"Workflow:{request.Action}", "case not found or access denied", request.ActorId, request.CaseId);
            return Result.Fail(Error.NotFound(ApiMessages.CaseNotFound));
        }

        if (!string.IsNullOrWhiteSpace(request.InternalComment) &&
            !authorizationService.HasPermission(CasePermissions.CreateInternalComment))
        {
            ApplicationLog.Blocked(logger, $"Workflow:{request.Action}", "cannot create internal comment", request.ActorId, request.CaseId);
            return Result.Fail(Error.Forbidden(ApiMessages.NotAllowed));
        }

        var correlationId = ResolveCorrelationGuid(httpContextAccessor.HttpContext);
        var dispatch = await processManager.DispatchAsync(
            new ProcessCommand(
                CaseModuleType.Investment,
                request.CaseId,
                request.Action.ToString(),
                request.ActorId,
                request.ActorRole,
                correlationId,
                request.Comment,
                WorkflowSignals.StatusChanged,
                new InvestmentWorkflowCommandPayload(request.InternalComment)),
            cancellationToken);

        if (dispatch.IsFailure)
        {
            ApplicationLog.Blocked(logger, $"Workflow:{request.Action}",
                dispatch.Error?.Message ?? "process command rejected",
                request.ActorId, request.CaseId);
            return Result.Fail(dispatch.Error!);
        }

        ApplicationLog.Completed(logger,
            "User {UserId} (role {Role}) dispatched {Action} on investment case {CaseId} ({CaseNumber})",
            request.ActorId, request.ActorRole, request.Action, request.CaseId, entity.CaseNumber);

        return Result.Ok();
    }

    private static Guid ResolveCorrelationGuid(HttpContext? httpContext)
    {
        var raw = httpContext?.Items[CorrelationContext.ItemKey]?.ToString()
                  ?? httpContext?.Request.Headers[CorrelationContext.HeaderName].ToString()
                  ?? httpContext?.TraceIdentifier;

        if (string.IsNullOrWhiteSpace(raw))
            return Guid.NewGuid();

        if (Guid.TryParse(raw, out var parsed))
            return parsed;

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
        var guidBytes = bytes.Take(16).ToArray();
        return new Guid(guidBytes);
    }
}
