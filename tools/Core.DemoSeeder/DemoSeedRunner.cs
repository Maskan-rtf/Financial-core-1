using BuildingBlocks.Persistence.Db.DomainEvents;
using Core.DemoSeeder.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Core.DemoSeeder;

internal static class DemoSeedRunner
{
    public static async Task RunAsync(CancellationToken ct)
    {
        var configuration = BuildConfiguration();
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? Environment.GetEnvironmentVariable("CORE_POSTGRES_CONNECTION")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' not found. Set it in Core.API/appsettings.json or CORE_POSTGRES_CONNECTION.");

        Console.WriteLine("Connecting to Postgres...");
        Console.WriteLine(MaskConnectionString(connectionString));

        var features = await SchemaFeatures.DetectAsync(connectionString, ct);
        Console.WriteLine(
            $"Schema features: User.CompanyId={features.UserHasCompanyId}, " +
            $"FundLimits={features.HasFundCreditLimits}, " +
            $"ApplicantCredit={features.HasGuaranteeApplicantCreditProfiles}, " +
            $"Process={features.HasProcessInstances}");

        var options = new DbContextOptionsBuilder<DemoDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(5))
            .Options;

        await using var db = new DemoDbContext(options, new NoOpDomainEventDispatcher(), features);

        if (!await db.Database.CanConnectAsync(ct))
            throw new InvalidOperationException("Cannot connect to the database.");

        Console.WriteLine("Clearing existing application data (schema unchanged)...");
        await DatabaseCleaner.ClearAsync(db, ct);

        Console.WriteLine("Seeding identity (users & companies)...");
        IdentitySeeder.DemoActors actors;
        if (features.UserHasCompanyId)
        {
            actors = IdentitySeeder.Seed(db);
            await db.SaveChangesAsync(ct);

            actors.ApplicantNavaco.CompanyId = actors.Navaco.Id;
            actors.ApplicantParsa.CompanyId = actors.ParsaTech.Id;
            actors.ApplicantSepanta.CompanyId = actors.SepantaEnergy.Id;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            actors = await IdentitySqlSeeder.SeedAsync(connectionString, ct);
        }

        if (features.HasFundCreditLimits || features.HasGuaranteeApplicantCreditProfiles)
        {
            Console.WriteLine("Seeding fund credit limits & applicant credit profiles...");
            FundAndCreditSeeder.Seed(db, actors, features);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            Console.WriteLine("Skipping fund/applicant credit tables (not present on this database).");
        }

        Console.WriteLine("Seeding investment cases...");
        InvestmentSeeder.Seed(db, actors, features);
        await db.SaveChangesAsync(ct);

        Console.WriteLine("Seeding guarantee cases...");
        GuaranteeSeeder.Seed(db, actors, features);
        await db.SaveChangesAsync(ct);

        Console.WriteLine("Seeding loan cases...");
        LoanSeeder.Seed(db, actors);
        await db.SaveChangesAsync(ct);

        Console.WriteLine("Seeding dashboard snapshot...");
        DashboardSeeder.Seed(db);
        await db.SaveChangesAsync(ct);

        Console.WriteLine();
        Console.WriteLine("Demo seed completed successfully.");
        Console.WriteLine("────────────────────────────────────────");
        Console.WriteLine($"Admin login phone : {actors.Admin.PhoneNumber}");
        Console.WriteLine($"CEO               : {actors.Ceo.PhoneNumber} ({actors.Ceo.FirstName} {actors.Ceo.LastName})");
        Console.WriteLine($"Applicant (Navaco): {actors.ApplicantNavaco.PhoneNumber}");
        Console.WriteLine($"Companies         : {actors.Navaco.Name}");
        Console.WriteLine($"                  : {actors.ParsaTech.Name}");
        Console.WriteLine($"                  : {actors.SepantaEnergy.Name}");
        Console.WriteLine("────────────────────────────────────────");
    }

    private static IConfiguration BuildConfiguration()
    {
        var apiDir = FindApiDirectory();
        return new ConfigurationBuilder()
            .SetBasePath(apiDir)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string FindApiDirectory()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Services", "CoreService", "Core.API")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Services", "CoreService", "Core.API")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "src", "Services", "CoreService", "Core.API")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "src", "Services", "CoreService", "Core.API"))
        };

        foreach (var dir in candidates)
        {
            if (File.Exists(Path.Combine(dir, "appsettings.json")))
                return dir;
        }

        throw new DirectoryNotFoundException(
            "Could not locate Core.API/appsettings.json. Run from repo root or tools/Core.DemoSeeder.");
    }

    private static string MaskConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(';', parts.Select(p =>
            p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase)
                ? "Password=***"
                : p));
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(
            IReadOnlyCollection<BuildingBlocks.Domain.Events.IDomainEvent> domainEvents,
            CancellationToken ct)
            => Task.CompletedTask;
    }
}
