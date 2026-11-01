using Erp.Messaging.Outbox;
using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Core.Domain.BusinessPartners;
using Erp.Modules.Core.Domain.Companies;
using Erp.Modules.Core.Domain.UnitsOfMeasure;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Modules.Core.Infrastructure;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options), ICoreDbContext
{
    public const string Schema = "core";

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<BusinessPartner> BusinessPartners => Set<BusinessPartner>();

    public DbSet<Article> Articles => Set<Article>();

    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();

    public void AddIntegrationEvent(IIntegrationEvent integrationEvent) => Set<OutboxMessage>().Add(OutboxMessage.From(integrationEvent));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
        modelBuilder.AddOutbox();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<CompanyId>().HaveConversion<CompanyIdConverter>();
    }

    private sealed class CompanyIdConverter() : ValueConverter<CompanyId, Guid>(id => id.Value, value => new CompanyId(value));
}
