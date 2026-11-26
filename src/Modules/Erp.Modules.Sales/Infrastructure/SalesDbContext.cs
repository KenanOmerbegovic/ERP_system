using Erp.Messaging.Outbox;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.Modules.Sales.Domain.Invoices;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Sales.Domain.Offers;
using Erp.Modules.Sales.Domain.Orders;
using Erp.Modules.Sales.Domain.References;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Modules.Sales.Infrastructure;

public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options), ISalesDbContext
{
    public const string Schema = "sales";

    public DbSet<CustomerReference> Customers => Set<CustomerReference>();

    public DbSet<ArticleReference> Articles => Set<ArticleReference>();

    public DbSet<Offer> Offers => Set<Offer>();

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();

    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();

    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();

    public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();

    public void AddIntegrationEvent(IIntegrationEvent integrationEvent) => Set<OutboxMessage>().Add(OutboxMessage.From(integrationEvent));

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => Database.BeginTransactionAsync(cancellationToken);

    public void ClearTracking() => ChangeTracker.Clear();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
        modelBuilder.AddOutbox();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<CompanyId>().HaveConversion<CompanyIdConverter>();
    }

    private sealed class CompanyIdConverter() : ValueConverter<CompanyId, Guid>(id => id.Value, value => new CompanyId(value));
}
