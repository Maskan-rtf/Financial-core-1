using Core.DemoSeeder;

Console.OutputEncoding = System.Text.Encoding.UTF8;
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
