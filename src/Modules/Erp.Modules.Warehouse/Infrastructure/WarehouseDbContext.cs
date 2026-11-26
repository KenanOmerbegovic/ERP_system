using Erp.Messaging.Outbox;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Domain.Articles;
using Erp.Modules.Warehouse.Domain.Commands;
using Erp.Modules.Warehouse.Domain.Numbering;
using Erp.Modules.Warehouse.Domain.Stock;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Domain.Storehouses;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Modules.Warehouse.Infrastructure;

public sealed class WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : DbContext(options), IWarehouseDbContext
{
    public const string Schema = "warehouse";

    public DbSet<ArticleReference> Articles => Set<ArticleReference>();

    public DbSet<Storehouse> Storehouses => Set<Storehouse>();

    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<Lot> Lots => Set<Lot>();

    public DbSet<LotBalance> LotBalances => Set<LotBalance>();

    public DbSet<SerialNumber> SerialNumbers => Set<SerialNumber>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();

    public DbSet<CommandLogEntry> CommandLog => Set<CommandLogEntry>();

    public void AddIntegrationEvent(IIntegrationEvent integrationEvent) => Set<OutboxMessage>().Add(OutboxMessage.From(integrationEvent));

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WarehouseDbContext).Assembly);
        modelBuilder.AddOutbox();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<CompanyId>().HaveConversion<CompanyIdConverter>();
    }

    private sealed class CompanyIdConverter() : ValueConverter<CompanyId, Guid>(id => id.Value, value => new CompanyId(value));
}
