using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.DTOs;
using Core.Application.Requests;

namespace Core.Application.Services;

public sealed class GuaranteeRenewalAppService : IGuaranteeRenewalAppService
{
    public Task<Result<GuaranteeRenewalDto>> CreateAsync(CreateGuaranteeRenewalRequest request, CancellationToken ct)
        => Task.FromResult(Result<GuaranteeRenewalDto>.Fail(Deprecated()));

    public Task<Result<GuaranteeRenewalDto>> GetAsync(Guid id, CancellationToken ct)
        => Task.FromResult(Result<GuaranteeRenewalDto>.Fail(Deprecated()));

    public Task<Result> SubmitAsync(Guid id, CancellationToken ct)
        => Task.FromResult(Result.Fail(Deprecated()));

    public Task<Result> CeoApproveAsync(Guid id, CancellationToken ct)
        => Task.FromResult(Result.Fail(Deprecated()));

    public Task<Result> CeoRejectAsync(Guid id, string reason, CancellationToken ct)
        => Task.FromResult(Result.Fail(Deprecated()));

    public Task<Result> UpdateCreditDatesAsync(Guid id, UpdateGuaranteeRenewalDatesRequest request, CancellationToken ct)
        => Task.FromResult(Result.Fail(Deprecated()));

    private static Error Deprecated()
        => Error.Conflict(ApiMessages.GuaranteeRenewalDeprecatedUseAmendment);
}
