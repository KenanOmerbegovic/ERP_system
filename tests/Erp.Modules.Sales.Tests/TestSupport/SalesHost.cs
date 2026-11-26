using Erp.Localization.BiH.Vat;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Infrastructure;
using Erp.Modules.Warehouse;
using Erp.Modules.Warehouse.Infrastructure;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Erp.Modules.Sales.Tests.TestSupport;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}

/// <summary>Every company gets the same rates; Core's real lookup needs Core's database, which these tests do not use.</summary>
internal sealed class FakeVatRates : IVatRateLookup
{
    private static readonly VatRateInfo[] Rates =
    [
        new("PDV17", "Opća stopa", 0.17m, new DateOnly(2006, 1, 1)),
        new("OSL", "Oslobođeno", 0m, new DateOnly(2006, 1, 1)),
    ];

    public Task<VatRateInfo?> FindAsync(CompanyId companyId, string code, DateOnly date, CancellationToken cancellationToken) =>
        Task.FromResult(Rates.Where(r => r.Code == code.Trim() && r.ValidFrom <= date).MaxBy(r => r.ValidFrom));
}

/// <summary>
/// One throwaway PostgreSQL with the Warehouse and Sales schemas, and the two modules composed as the API composes them
/// (without HTTP and without running the outbox processors), shared by all tests in <see cref="SalesCollection"/>.
/// Tests isolate themselves with their own company. Needs Docker running.
/// </summary>
public sealed class SalesHost : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    internal FakeClock Clock { get; } = new();

    internal IServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Erp"] = _container.GetConnectionString(),
                ["Messaging:Outbox:PollInterval"] = "00:00:01",
                ["Messaging:Outbox:MaxIdlePollInterval"] = "00:00:05",
                ["Messaging:Outbox:BatchSize"] = "20",
                ["Messaging:Outbox:MaxAttempts"] = "5",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IVatRateLookup, FakeVatRates>();
        services.AddScoped<IVatCalculator, BiHVatCalculator>();
        services.AddWarehouseModule(configuration);
        services.AddSalesModule(configuration);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<WarehouseDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SalesDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Services is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SalesCollection : ICollectionFixture<SalesHost>
{
    public const string Name = "Sales database";
}
