using Core.Domain.Identity;
using Core.Domain.Identity.Entities;
using Npgsql;

namespace Core.DemoSeeder.Seed;

internal static class IdentitySqlSeeder
{
    /// <summary>
    /// Inserts users/companies without referencing Identity.User.CompanyId (absent on FinancialCore).
    /// </summary>
    public static async Task<IdentitySeeder.DemoActors> SeedAsync(string connectionString, CancellationToken ct)
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

        var users = new[]
        {
            admin, ceo, invExpert, invManager, legalExpert, legalManager,
            finExpert, finManager, creditExpert, creditManager, techExpert, techManager,
            applicantNavaco, applicantParsa, applicantSepanta, applicantIndividual
        };

        var navaco = Company(DemoIds.Company("navaco"), applicantNavaco.Id, "شرکت دانش‌بنیان نوآوران صنعت ناوکو", "41111234567", "145698", "10101234567", "02188776655", "تهران، خیابان سهروردی شمالی، پلاک ۱۲۸", "تهران", "تهران", "1557612345");
        var parsa = Company(DemoIds.Company("parsa"), applicantParsa.Id, "شرکت پارساتک پردازش داده", "41119876543", "158742", "10109876543", "02144556677", "تهران، شهرک غرب، خیابان ایران‌زمین، برج آسمان، طبقه ۸", "تهران", "تهران", "1469611122");
        var sepanta = Company(DemoIds.Company("sepanta"), applicantSepanta.Id, "شرکت انرژی پاک سپنتا", "41115554433", "167301", "10105554433", "03132221100", "اصفهان، خیابان هزارجریب، شهرک علمی و تحقیقاتی، واحد ۴۱", "اصفهان", "اصفهان", "8174678901");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        foreach (var user in users)
        {
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO "Identity"."User"
                  ("Id","PhoneNumber","Email","FirstName","LastName","NationalCode","Role","IsActive","IsPhoneVerified","CreatedAt","LastLoginAt","CreateDate","UpdateDate","IsDeleted")
                VALUES
                  (@Id,@PhoneNumber,@Email,@FirstName,@LastName,@NationalCode,@Role,@IsActive,@IsPhoneVerified,@CreatedAt,@LastLoginAt,@CreateDate,@UpdateDate,@IsDeleted);
                """, conn, tx);
            cmd.Parameters.AddWithValue("Id", user.Id);
            cmd.Parameters.AddWithValue("PhoneNumber", user.PhoneNumber);
            cmd.Parameters.AddWithValue("Email", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("FirstName", user.FirstName);
            cmd.Parameters.AddWithValue("LastName", user.LastName);
            cmd.Parameters.AddWithValue("NationalCode", (object?)user.NationalCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("Role", (int)user.Role);
            cmd.Parameters.AddWithValue("IsActive", user.IsActive);
            cmd.Parameters.AddWithValue("IsPhoneVerified", user.IsPhoneVerified);
            cmd.Parameters.AddWithValue("CreatedAt", user.CreatedAt);
            cmd.Parameters.AddWithValue("LastLoginAt", (object?)user.LastLoginAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("CreateDate", user.CreateDate);
            cmd.Parameters.AddWithValue("UpdateDate", (object?)user.UpdateDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("IsDeleted", user.IsDeleted);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        foreach (var company in new[] { navaco, parsa, sepanta })
        {
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO "Identity"."Company"
                  ("Id","OwnerUserId","Name","EconomicCode","RegistrationNumber","NationalId","PhoneNumber","Address","City","Province","PostalCode","CreateDate","UpdateDate","IsDeleted")
                VALUES
                  (@Id,@OwnerUserId,@Name,@EconomicCode,@RegistrationNumber,@NationalId,@PhoneNumber,@Address,@City,@Province,@PostalCode,@CreateDate,@UpdateDate,@IsDeleted);
                """, conn, tx);
            cmd.Parameters.AddWithValue("Id", company.Id);
            cmd.Parameters.AddWithValue("OwnerUserId", company.OwnerUserId);
            cmd.Parameters.AddWithValue("Name", company.Name);
            cmd.Parameters.AddWithValue("EconomicCode", company.EconomicCode);
            cmd.Parameters.AddWithValue("RegistrationNumber", (object?)company.RegistrationNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("NationalId", (object?)company.NationalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("PhoneNumber", (object?)company.PhoneNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("Address", (object?)company.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("City", (object?)company.City ?? DBNull.Value);
            cmd.Parameters.AddWithValue("Province", (object?)company.Province ?? DBNull.Value);
            cmd.Parameters.AddWithValue("PostalCode", (object?)company.PostalCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("CreateDate", company.CreateDate);
            cmd.Parameters.AddWithValue("UpdateDate", (object?)company.UpdateDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("IsDeleted", company.IsDeleted);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        return new IdentitySeeder.DemoActors(
            admin, ceo, invExpert, invManager, legalExpert, legalManager,
            finExpert, finManager, creditExpert, creditManager, techExpert, techManager,
            applicantNavaco, applicantParsa, applicantSepanta, applicantIndividual,
            navaco, parsa, sepanta);
    }

    private static User Staff(Guid id, string phone, string firstName, string lastName, UserRole role, string nationalCode, string email)
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
            LastLoginAt = DateTime.UtcNow.AddDays(-1),
            CreateDate = DateTime.UtcNow.AddMonths(-8)
        };

    private static User Applicant(Guid id, string phone, string firstName, string lastName, string nationalCode, string email)
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
            LastLoginAt = DateTime.UtcNow.AddHours(-6),
            CreateDate = DateTime.UtcNow.AddMonths(-5)
        };

    private static Company Company(
        Guid id, Guid ownerUserId, string name, string economicCode, string registrationNumber,
        string nationalId, string phone, string address, string city, string province, string postalCode)
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
