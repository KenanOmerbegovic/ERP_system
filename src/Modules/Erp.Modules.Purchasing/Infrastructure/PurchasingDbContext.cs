using Erp.Messaging.Outbox;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Domain.Invoices;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.Modules.Purchasing.Domain.References;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Modules.Purchasing.Infrastructure;

public sealed class PurchasingDbContext(DbContextOptions<PurchasingDbContext> options) : DbContext(options), IPurchasingDbContext
{
    public const string Schema = "purchasing";

    public DbSet<SupplierReference> Suppliers => Set<SupplierReference>();

    public DbSet<ArticleReference> Articles => Set<ArticleReference>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();

    public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();

    public void AddIntegrationEvent(IIntegrationEvent integrationEvent) => Set<OutboxMessage>().Add(OutboxMessage.From(integrationEvent));

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => Database.BeginTransactionAsync(cancellationToken);

    public void ClearTracking() => ChangeTracker.Clear();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PurchasingDbContext).Assembly);
        modelBuilder.AddOutbox();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<CompanyId>().HaveConversion<CompanyIdConverter>();
    }

    private sealed class CompanyIdConverter() : ValueConverter<CompanyId, Guid>(id => id.Value, value => new CompanyId(value));
}
