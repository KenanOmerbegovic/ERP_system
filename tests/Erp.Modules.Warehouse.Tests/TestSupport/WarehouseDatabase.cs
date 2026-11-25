using Erp.Modules.Warehouse.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Erp.Modules.Warehouse.Tests.TestSupport;

/// <summary>
/// One throwaway PostgreSQL (same image as docker-compose.yml) with the Warehouse schema, shared by all tests in
/// <see cref="WarehouseCollection"/>. Real PostgreSQL is needed: posting relies on row locks and check constraints.
/// Tests isolate themselves with their own company. Needs Docker running.
/// </summary>
public sealed class WarehouseDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A new context, like one HTTP request gets.</summary>
    public WarehouseDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", WarehouseDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options);
}

[CollectionDefinition(Name)]
public sealed class WarehouseCollection : ICollectionFixture<WarehouseDatabase>
{
    public const string Name = "Warehouse database";
}
