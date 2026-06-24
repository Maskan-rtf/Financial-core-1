using Core.Application.Abstractions;
using Core.Application.Common;

namespace Core.Application.Services;

public sealed class LoanCaseNumberGenerator : ILoanCaseNumberGenerator
{
    public Task<string> GenerateLoanCaseAsync(CancellationToken cancellationToken = default, int dailySequence = 1)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CaseNumberFormat.Build("LN", DateTimeOffset.UtcNow, dailySequence));
    }
}
