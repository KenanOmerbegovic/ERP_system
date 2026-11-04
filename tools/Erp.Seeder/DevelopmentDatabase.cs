using Erp.Modules.Core;
using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Infrastructure;
using Erp.Modules.Identity;
using Erp.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Erp.Seeder;

/// <summary>Readies the database before seeding. Only local databases are accepted, because --reset deletes data.</summary>
internal static class DevelopmentDatabase
{
    private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "::1"];

    private const string DropSchemas =
        "DROP SCHEMA IF EXISTS " + CoreDbContext.Schema + " CASCADE; " +
        "DROP SCHEMA IF EXISTS " + IdentityDbContext.Schema + " CASCADE;";

    public static async Task PrepareAsync(IServiceProvider services, string companyTaxId, bool reset, CancellationToken cancellationToken)
    {
        var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString(CoreModule.ConnectionStringName);
        var host = new NpgsqlConnectionStringBuilder(connectionString).Host;
        if (host is null || !LocalHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            throw new SeedFailedException($"The seeder only writes to a local database; the connection string points at '{host}'.");
        }

        if (reset)
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<CoreDbContext>().Database;

            // A database that does not exist yet has nothing to drop; migrating creates it.
            if (await database.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken))
            {
                await database.ExecuteSqlRawAsync(DropSchemas, cancellationToken);
            }
        }

        await services.MigrateCoreDatabaseAsync(cancellationToken);
        await services.MigrateIdentityDatabaseAsync(cancellationToken);

        await using (var scope = services.CreateAsyncScope())
        {
            var core = scope.ServiceProvider.GetRequiredService<ICoreDbContext>();
            if (await core.Companies.AnyAsync(company => company.TaxId == companyTaxId, cancellationToken))
            {
                throw new SeedFailedException($"The mock company (JIB {companyTaxId}) is already seeded. Run with --reset to start over.");
            }
        }
    }
}
