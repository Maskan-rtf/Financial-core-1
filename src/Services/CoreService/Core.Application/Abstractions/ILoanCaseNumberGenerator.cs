namespace Core.Application.Abstractions;

public interface ILoanCaseNumberGenerator
{
    Task<string> GenerateLoanCaseAsync(CancellationToken cancellationToken = default, int dailySequence = 1);
}
