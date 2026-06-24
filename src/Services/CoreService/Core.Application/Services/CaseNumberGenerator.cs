using Core.Application.Abstractions;
using Core.Application.Common;

namespace Core.Application.Services;

public sealed class CaseNumberGenerator : ICaseNumberGenerator
{
    public Task<string> GenerateAsync(CancellationToken cancellationToken, int dailySequence = 1)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CaseNumberFormat.Build("IC", DateTimeOffset.UtcNow, dailySequence));
    }
}
