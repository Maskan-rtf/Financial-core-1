using Core.Domain.Identity;
using Core.Domain.Identity.Entities;

namespace Core.DemoSeeder.Seed;

internal static class IdentitySeeder
{
    public sealed record DemoActors(
        User Admin,
        User Ceo,
        User InvestmentExpert,
        User InvestmentManager,
        User LegalExpert,
        User LegalManager,
        User FinancialExpert,
        User FinancialManager,
        User CreditExpert,
        User CreditManager,
        User TechnicalExpert,
        User TechnicalManager,
        User ApplicantNavaco,
        User ApplicantParsa,
        User ApplicantSepanta,
        User ApplicantIndividual,
        Company Navaco,
        Company ParsaTech,
        Company SepantaEnergy);

    public static DemoActors Seed(DemoDbContext db)
    {
        var admin = Staff(DemoIds.User("admin"), "09358357345", "آرش", "محمدی", UserRole.Admin, "0012345678", "arash.mohammadi@rtf-fund.ir");
        var ceo = Staff(DemoIds.User("ceo"), "09121234501", "نادر", "کاظمی", UserRole.Ceo, "0012345679", "nader.kazemi@rtf-fund.ir");
        var invExpert = Staff(DemoIds.User("inv-expert"), "09121234502", "سارا", "احمدی", UserRole.InvestmentExpert, "0012345680", "sara.ahmadi@rtf-fund.ir");
        var invManager = Staff(DemoIds.User("inv-manager"), "09121234503", "رضا", "حسینی", UserRole.InvestmentManager, "0012345681", "reza.hosseini@rtf-fund.ir");
        var legalExpert = Staff(DemoIds.User("legal-expert"), "09121234504", "مریم", "رضایی", UserRole.LegalExpert, "0012345682", "maryam.rezaei@rtf-fund.ir");
        var legalManager = Staff(DemoIds.User("legal-manager"), "09121234505", "حسین", "کریمی", UserRole.LegalManager, "0012345683", "hossein.karimi@rtf-fund.ir");
        var finExpert = Staff(DemoIds.User("fin-expert"), "09121234506", "نیلوفر", "موسوی", UserRole.FinancialExpert, "0012345684", "niloufar.mousavi@rtf-fund.ir");
        var finManager = Staff(DemoIds.User("fin-manager"), "09121234507", "امیر", "جعفری", UserRole.FinancialManager, "0012345685", "amir.jafari@rtf-fund.ir");
        var creditExpert = Staff(DemoIds.User("credit-expert"), "09121234508", "الهام", "نوری", UserRole.CreditExpert, "0012345686", "elham.nouri@rtf-fund.ir");
        var creditManager = Staff(DemoIds.User("credit-manager"), "09121234509", "بهرام", "صادقی", UserRole.CreditManager, "0012345687", "bahram.sadeghi@rtf-fund.ir");
        var techExpert = Staff(DemoIds.User("tech-expert"), "09121234510", "پویا", "اکبری", UserRole.TechnicalExpert, "0012345688", "pouya.akbari@rtf-fund.ir");
        var techManager = Staff(DemoIds.User("tech-manager"), "09121234511", "شیما", "فرهادی", UserRole.TechnicalManager, "0012345689", "shima.farhadi@rtf-fund.ir");

        var applicantNavaco = Applicant(DemoIds.User("app-navaco"), "09131112201", "کامران", "یزدانی", "0076543210", "kamran.yazdani@navaco.ir");
        var applicantParsa = Applicant(DemoIds.User("app-parsa"), "09131112202", "لیلا", "طاهری", "0076543211", "leila.taheri@parsatech.ir");
        var applicantSepanta = Applicant(DemoIds.User("app-sepanta"), "09131112203", "مجید", "قنبری", "0076543212", "majid.ghanbari@sepanta-energy.ir");
        var applicantIndividual = Applicant(DemoIds.User("app-individual"), "09131112204", "فرناز", "مرادی", "0076543213", "farnaz.moradi@gmail.com");

        db.Users.AddRange(
            admin, ceo, invExpert, invManager, legalExpert, legalManager,
            finExpert, finManager, creditExpert, creditManager, techExpert, techManager,
            applicantNavaco, applicantParsa, applicantSepanta, applicantIndividual);

        var navaco = Company(
            DemoIds.Company("navaco"),
            applicantNavaco.Id,
            "شرکت دانش‌بنیان نوآوران صنعت ناوکو",
            "41111234567",
            "145698",
            "10101234567",
            "02188776655",
            "تهران، خیابان سهروردی شمالی، پلاک ۱۲۸",
            "تهران",
            "تهران",
            "1557612345");

        var parsa = Company(
            DemoIds.Company("parsa"),
            applicantParsa.Id,
            "شرکت پارساتک پردازش داده",
            "41119876543",
            "158742",
            "10109876543",
            "02144556677",
            "تهران، شهرک غرب، خیابان ایران‌زمین، برج آسمان، طبقه ۸",
            "تهران",
            "تهران",
            "1469611122");

        var sepanta = Company(
            DemoIds.Company("sepanta"),
            applicantSepanta.Id,
            "شرکت انرژی پاک سپنتا",
            "41115554433",
            "167301",
            "10105554433",
            "03132221100",
            "اصفهان، خیابان هزارجریب، شهرک علمی و تحقیقاتی، واحد ۴۱",
            "اصفهان",
            "اصفهان",
            "8174678901");

        db.Companies.AddRange(navaco, parsa, sepanta);

        // CompanyId is assigned after the first SaveChanges to avoid User↔Company insert cycles.
        return new DemoActors(
            admin, ceo, invExpert, invManager, legalExpert, legalManager,
            finExpert, finManager, creditExpert, creditManager, techExpert, techManager,
            applicantNavaco, applicantParsa, applicantSepanta, applicantIndividual,
            navaco, parsa, sepanta);
    }

    private static User Staff(
        Guid id,
        string phone,
        string firstName,
        string lastName,
        UserRole role,
        string nationalCode,
        string email)
        => new()
        {
            Id = id,
            PhoneNumber = phone,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            NationalCode = nationalCode,
            Email = email,
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-8),
            LastLoginAt = DateTime.UtcNow.AddDays(-1)
        };

    private static User Applicant(
        Guid id,
        string phone,
        string firstName,
        string lastName,
        string nationalCode,
        string email)
        => new()
        {
            Id = id,
            PhoneNumber = phone,
            FirstName = firstName,
            LastName = lastName,
            Role = UserRole.Applicant,
            NationalCode = nationalCode,
            Email = email,
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-5),
            LastLoginAt = DateTime.UtcNow.AddHours(-6)
        };

    private static Company Company(
        Guid id,
        Guid ownerUserId,
        string name,
        string economicCode,
        string registrationNumber,
        string nationalId,
        string phone,
        string address,
        string city,
        string province,
        string postalCode)
        => new()
        {
            Id = id,
            OwnerUserId = ownerUserId,
            Name = name,
            EconomicCode = economicCode,
            RegistrationNumber = registrationNumber,
            NationalId = nationalId,
            PhoneNumber = phone,
            Address = address,
            City = city,
            Province = province,
            PostalCode = postalCode,
            CreateDate = DateTime.UtcNow.AddYears(-1)
        };
}
