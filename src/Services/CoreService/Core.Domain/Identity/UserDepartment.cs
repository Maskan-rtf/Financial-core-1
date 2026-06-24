namespace Core.Domain.Identity;

public enum UserDepartment
{
    Investment = 1,
    Credit = 2,
    Legal = 3,
    Financial = 4,
    Technical = 5
}

public static class UserRoleDepartments
{
    public static UserDepartment? GetUserDepartment(UserRole role) =>
        role switch
        {
            UserRole.InvestmentExpert or UserRole.InvestmentManager => UserDepartment.Investment,
            UserRole.CreditExpert or UserRole.CreditManager => UserDepartment.Credit,
            UserRole.LegalExpert or UserRole.LegalManager => UserDepartment.Legal,
            UserRole.FinancialExpert or UserRole.FinancialManager => UserDepartment.Financial,
            UserRole.TechnicalExpert or UserRole.TechnicalManager => UserDepartment.Technical,
            _ => null
        };

    public static bool TryGetUserDepartment(UserRole role, out UserDepartment department)
    {
        var resolved = GetUserDepartment(role);
        if (resolved.HasValue)
        {
            department = resolved.Value;
            return true;
        }

        department = default;
        return false;
    }

    public static bool TryGetUserDepartment(string? claim, out UserDepartment department)
    {
        department = default;

        if (!UserRoleClaims.TryParse(claim, out var role))
            return false;

        return TryGetUserDepartment(role, out department);
    }
}
