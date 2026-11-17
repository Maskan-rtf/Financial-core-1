using BuildingBlocks.Persistence.Db.DomainEvents;
using Core.Domain.Identity;
using Core.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Core.DemoSeeder;

internal static class WipeAdminOnlyRunner
{
    private const string AdminPhone = "09915097638";

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
        var options = new DbContextOptionsBuilder<DemoDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(5))
            .Options;

        await using var db = new DemoDbContext(options, new NoOpDomainEventDispatcher(), features);

        if (!await db.Database.CanConnectAsync(ct))
            throw new InvalidOperationException("Cannot connect to the database.");

        Console.WriteLine("Clearing all application data...");
        await DatabaseCleaner.ClearAsync(db, ct);
        await ClearExtraSchemasAsync(connectionString, ct);

        Console.WriteLine($"Creating system admin ({AdminPhone})...");
        var admin = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = AdminPhone,
            FirstName = "مدیر",
            LastName = "سیستم",
            Role = UserRole.Admin,
            Email = null,
            NationalCode = null,
            PasswordHash = SetAdminPasswordRunner.HashPassword("Amir2779@"),
            CompanyId = null,
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = null,
            IsDeleted = false
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        Console.WriteLine();
        Console.WriteLine("Wipe completed. Database now contains only:");
        Console.WriteLine($"  Admin phone : {admin.PhoneNumber}");
        Console.WriteLine($"  Admin role  : {admin.Role}");
        Console.WriteLine($"  Admin id    : {admin.Id}");
    }

    private static async Task ClearExtraSchemasAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Elsa / workflow runtime tables (if present) — wipe instance data too.
        var schemas = new[] { "Elsa", "elsa", "Workflow", "Hangfire" };
        var tables = new List<string>();

        await using (var cmd = new NpgsqlCommand("""
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
              AND table_schema = ANY(@schemas);
            """, conn))
        {
            cmd.Parameters.AddWithValue("schemas", schemas);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                tables.Add($"\"{reader.GetString(0)}\".\"{reader.GetString(1)}\"");
        }

        if (tables.Count == 0)
            return;

        Console.WriteLine($"Clearing {tables.Count} extra runtime table(s)...");
        await using var truncate = new NpgsqlCommand(
            $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE;",
            conn);
        await truncate.ExecuteNonQueryAsync(ct);
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
