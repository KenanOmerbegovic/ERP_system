using Erp.Seeder.Data;
using Microsoft.Extensions.Hosting;

namespace Erp.Seeder;

/// <summary>One seeding run: prepare the database, seed through the modules, check the result, print the logins.</summary>
internal static class SeedRun
{
    public const int Succeeded = 0;
    public const int Failed = 1;

    public static async Task<int> ExecuteAsync(IHost host, SeedOptions options, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        var data = SeedDataGenerator.Generate(options.Profile);
        try
        {
            await DevelopmentDatabase.PrepareAsync(host.Services, data.Company.TaxId, options.Reset, cancellationToken);

            // Started only now: the outbox processor needs the migrated tables.
            await host.StartAsync(cancellationToken);
            try
            {
                var seeded = await new MockDataSeeder(host.Services).SeedAsync(data, options.Password, cancellationToken);
                var problems = await SeedChecks.RunAsync(host.Services, data, seeded, cancellationToken);
                if (problems.Count > 0)
                {
                    await output.WriteLineAsync("Self-checks failed:");
                    foreach (var problem in problems)
                    {
                        await output.WriteLineAsync($"  {problem}");
                    }

                    return Failed;
                }
            }
            finally
            {
                await host.StopAsync(CancellationToken.None);
            }
        }
        catch (SeedFailedException failure)
        {
            await output.WriteLineAsync(failure.Message);
            if (!options.Reset)
            {
                await output.WriteLineAsync("If an earlier run stopped halfway, run again with --reset.");
            }

            return Failed;
        }

        await PrintSummaryAsync(data, options.Size, output);
        return Succeeded;
    }

    private static async Task PrintSummaryAsync(SeedData data, SeedSize size, TextWriter output)
    {
        await output.WriteLineAsync(
            $"Seeded {data.Company.Name} (JIB {data.Company.TaxId}, size {size.ToString().ToLowerInvariant()}): " +
            $"{data.Partners.Count} business partners, {data.Articles.Count} articles, {data.Units.Count} units of measure. Self-checks passed.");
        await output.WriteLineAsync("Users (password as given):");
        await output.WriteLineAsync($"  {data.Administrator.Email,-40} Administrator");
        foreach (var role in data.Roles)
        {
            await output.WriteLineAsync($"  {role.User.Email,-40} {role.Name}");
        }
    }
}
