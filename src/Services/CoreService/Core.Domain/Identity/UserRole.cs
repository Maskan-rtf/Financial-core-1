namespace Core.Domain.Identity;

public enum UserRole
{
    Applicant = 1,

    InvestmentExpert = 10,
    InvestmentManager = 11,

    Ceo = 12,
    BoardMember = 13,

    LegalExpert = 20,
    LegalManager = 21,

    FinancialExpert = 30,
    FinancialManager = 31,

    TechnicalExpert = 40,
    TechnicalManager = 41,

    CreditExpert = 50,
    CreditManager = 51,

    Admin = 100
}

public static class UserRoleClaims
{
    public const string Applicant = nameof(UserRole.Applicant);
    public const string InvestmentExpert = nameof(UserRole.InvestmentExpert);
    public const string InvestmentManager = nameof(UserRole.InvestmentManager);
    public const string Ceo = nameof(UserRole.Ceo);
    public const string BoardMember = nameof(UserRole.BoardMember);
    public const string Admin = nameof(UserRole.Admin);
    public const string LegalExpert = nameof(UserRole.LegalExpert);
    public const string LegalManager = nameof(UserRole.LegalManager);
    public const string FinancialExpert = nameof(UserRole.FinancialExpert);
    public const string FinancialManager = nameof(UserRole.FinancialManager);
    public const string TechnicalExpert = nameof(UserRole.TechnicalExpert);
    public const string TechnicalManager = nameof(UserRole.TechnicalManager);
    public const string CreditExpert = nameof(UserRole.CreditExpert);
    public const string CreditManager = nameof(UserRole.CreditManager);

    /// <summary>Legacy JWT aliases (pre expert/manager split).</summary>
    public const string LegalUnit = LegalExpert;

    public const string FinancialUnit = FinancialExpert;
    public const string InvestmentUnit = InvestmentExpert;

    public static string From(UserRole role) => role.ToString();

    public static string Normalize(string claim)
    {
        if (claim.Equals(LegalUnit, StringComparison.OrdinalIgnoreCase))
            return LegalExpert;
        if (claim.Equals(FinancialUnit, StringComparison.OrdinalIgnoreCase))
            return FinancialExpert;
        if (claim.Equals(InvestmentUnit, StringComparison.OrdinalIgnoreCase))
            return InvestmentExpert;
        if (claim.Equals("User", StringComparison.OrdinalIgnoreCase))
            return Applicant;
        return claim;
    }

    public static bool TryParse(string? claim, out UserRole role)
    {
        role = default;
        if (string.IsNullOrWhiteSpace(claim))
            return false;

        claim = Normalize(claim);

        foreach (UserRole value in Enum.GetValues<UserRole>())
        {
            if (claim.Equals(value.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                role = value;
                return true;
            }
        }

        return false;
    }
}
