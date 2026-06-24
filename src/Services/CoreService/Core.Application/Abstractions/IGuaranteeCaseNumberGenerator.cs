namespace Core.Application.Abstractions;

public interface IGuaranteeCaseNumberGenerator
{
    Task<string> GenerateGuaranteeCaseAsync(CancellationToken cancellationToken = default, int dailySequence = 1);
    Task<string> GenerateRenewalCaseAsync(CancellationToken cancellationToken = default, int dailySequence = 1);
}
