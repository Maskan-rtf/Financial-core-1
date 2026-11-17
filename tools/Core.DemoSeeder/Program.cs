using Core.DemoSeeder;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var setPasswordIndex = Array.FindIndex(args, a =>
    string.Equals(a, "--set-admin-password", StringComparison.OrdinalIgnoreCase));
if (setPasswordIndex >= 0)
{
    var password = setPasswordIndex + 1 < args.Length ? args[setPasswordIndex + 1] : "Amir2779@";
    Console.WriteLine("RTF Financial Core — Set admin password");
    Console.WriteLine();
    try
    {
        await SetAdminPasswordRunner.RunAsync(password, CancellationToken.None);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("Set password failed:");
        Console.Error.WriteLine(ex.ToString());
        return 1;
    }
}

var adminOnly = args.Any(a =>
    string.Equals(a, "--admin-only", StringComparison.OrdinalIgnoreCase)
    || string.Equals(a, "admin-only", StringComparison.OrdinalIgnoreCase)
    || string.Equals(a, "--wipe-admin", StringComparison.OrdinalIgnoreCase));

if (adminOnly)
{
    Console.WriteLine("RTF Financial Core — Wipe database (admin only)");
    Console.WriteLine("This will DELETE all application data and keep only system admin 09915097638.");
    Console.WriteLine("Schema / migrations will not be modified.");
    Console.WriteLine();

    try
    {
        await WipeAdminOnlyRunner.RunAsync(CancellationToken.None);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("Wipe failed:");
        Console.Error.WriteLine(ex.ToString());
        return 1;
    }
}

Console.WriteLine("RTF Financial Core — Professional Demo Database Seeder");
Console.WriteLine("This will REPLACE application data on the configured Postgres database.");
Console.WriteLine("Schema / migrations will not be modified.");
Console.WriteLine();

try
{
    await DemoSeedRunner.RunAsync(CancellationToken.None);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Seed failed:");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}
