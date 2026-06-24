using Core.Application.Abstractions;
using Core.Application.Common;

namespace Core.Application.Services;

public sealed class GuaranteeCaseNumberGenerator : IGuaranteeCaseNumberGenerator
{
    public Task<string> GenerateGuaranteeCaseAsync(CancellationToken cancellationToken = default, int dailySequence = 1)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CaseNumberFormat.Build("GC", DateTimeOffset.UtcNow, dailySequence));
    }

    public Task<string> GenerateRenewalCaseAsync(CancellationToken cancellationToken = default, int dailySequence = 1)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CaseNumberFormat.Build("GR", DateTimeOffset.UtcNow, dailySequence));
    }
}
