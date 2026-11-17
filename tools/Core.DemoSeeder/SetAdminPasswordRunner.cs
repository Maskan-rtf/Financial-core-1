using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Core.DemoSeeder;

internal static class SetAdminPasswordRunner
{
    private const string AdminPhone = "09915097638";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 10000;

    public static async Task RunAsync(string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required.", nameof(password));

        var configuration = BuildConfiguration();
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? Environment.GetEnvironmentVariable("CORE_POSTGRES_CONNECTION")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' not found. Set it in Core.API/appsettings.json or CORE_POSTGRES_CONNECTION.");

        var hash = HashPassword(password);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand("""
            UPDATE "Identity"."User"
            SET "PasswordHash" = @hash
            WHERE "PhoneNumber" = @phone
              AND "IsDeleted" = false;
            """, conn);
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("phone", AdminPhone);

        var updated = await cmd.ExecuteNonQueryAsync(ct);
        if (updated == 0)
            throw new InvalidOperationException($"Admin user with phone {AdminPhone} was not found.");

        Console.WriteLine($"Password set for admin {AdminPhone}.");
    }

    public static string HashPassword(string password)
    {
        using var algorithm = new Rfc2898DeriveBytes(password, SaltSize, Iterations, HashAlgorithmName.SHA256);
        var key = Convert.ToBase64String(algorithm.GetBytes(KeySize));
        var salt = Convert.ToBase64String(algorithm.Salt);
        return $"{Iterations}.{salt}.{key}";
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
}
