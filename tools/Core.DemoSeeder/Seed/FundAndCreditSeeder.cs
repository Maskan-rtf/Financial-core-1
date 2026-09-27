using Core.Domain.Entities.Fund;
using Core.Domain.Entities.Guarantee;
using Core.Domain.Enums;

namespace Core.DemoSeeder.Seed;

internal static class FundAndCreditSeeder
{
    public static void Seed(DemoDbContext db, IdentitySeeder.DemoActors actors, SchemaFeatures features)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yearStart = new DateOnly(today.Year, 1, 1);
        var yearEnd = new DateOnly(today.Year, 12, 31);

        if (features.HasFundCreditLimits)
        {
            db.FundCreditLimits.AddRange(
                new FundCreditLimit(
                    FundModuleType.Guarantee,
                    creditLimitWithCheck: 500_000_000_000m,
                    periodStart: yearStart,
                    expiresAt: yearEnd,
                    setByUserId: actors.Ceo.Id.ToString()),
                new FundCreditLimit(
                    FundModuleType.Loan,
                    creditLimitWithCheck: 350_000_000_000m,
                    periodStart: yearStart,
                    expiresAt: yearEnd,
                    setByUserId: actors.Ceo.Id.ToString()));
        }

        if (features.HasGuaranteeApplicantCreditProfiles)
        {
            db.GuaranteeApplicantCreditProfiles.AddRange(
                new GuaranteeApplicantCreditProfile(
                    actors.ApplicantNavaco.Id.ToString(),
                    actors.Navaco.Id,
                    80_000_000_000m,
                    actors.Ceo.Id.ToString()),
                new GuaranteeApplicantCreditProfile(
                    actors.ApplicantParsa.Id.ToString(),
                    actors.ParsaTech.Id,
                    45_000_000_000m,
                    actors.Ceo.Id.ToString()),
                new GuaranteeApplicantCreditProfile(
                    actors.ApplicantSepanta.Id.ToString(),
                    actors.SepantaEnergy.Id,
                    60_000_000_000m,
                    actors.Ceo.Id.ToString()),
                new GuaranteeApplicantCreditProfile(
                    actors.ApplicantIndividual.Id.ToString(),
                    companyId: null,
                    creditLimitWithCheck: 5_000_000_000m,
                    setByUserId: actors.Ceo.Id.ToString()));
        }
    }
}
