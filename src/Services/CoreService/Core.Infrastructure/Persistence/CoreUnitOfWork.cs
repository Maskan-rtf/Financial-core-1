using Core.Application.Abstractions;
using Core.Application.Abstractions.Persistence;
using Core.Application.Identity.Abstractions;
using Core.Persistence;

namespace Core.Infrastructure.Persistence;

public sealed class CoreUnitOfWork(
    CoreDbContext context,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUserSessionRepository userSessions,
    ICompanyRepository companies,
    IInvestmentCaseRepository investmentCases,
    IGuaranteeCaseRepository guaranteeCases,
    IGuaranteeRenewalCaseRepository guaranteeRenewals,
    ILoanCaseRepository loanCases,
    IFundCreditLimitRepository fundCreditLimits) : ICoreUnitOfWork
{
    public IUserRepository Users => users;
    public IRefreshTokenRepository RefreshTokens => refreshTokens;
    public IUserSessionRepository UserSessions => userSessions;
    public ICompanyRepository Companies => companies;
    public IInvestmentCaseRepository InvestmentCases => investmentCases;
    public IGuaranteeCaseRepository GuaranteeCases => guaranteeCases;
    public IGuaranteeRenewalCaseRepository GuaranteeRenewals => guaranteeRenewals;
    public ILoanCaseRepository LoanCases => loanCases;
    public IFundCreditLimitRepository FundCreditLimits => fundCreditLimits;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    public void Dispose()
    {
    }
}
