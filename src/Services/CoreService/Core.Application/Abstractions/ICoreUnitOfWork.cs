using Core.Application.Abstractions.Persistence;
using Core.Application.Identity.Abstractions;

namespace Core.Application.Abstractions;

public interface ICoreUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IUserSessionRepository UserSessions { get; }
    ICompanyRepository Companies { get; }
    IInvestmentCaseRepository InvestmentCases { get; }
    IGuaranteeCaseRepository GuaranteeCases { get; }
    IGuaranteeRenewalCaseRepository GuaranteeRenewals { get; }
    ILoanCaseRepository LoanCases { get; }
    IFundCreditLimitRepository FundCreditLimits { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
