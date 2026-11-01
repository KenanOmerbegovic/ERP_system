using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Core.Domain.BusinessPartners;
using Erp.Modules.Core.Domain.Companies;
using Erp.Modules.Core.Domain.UnitsOfMeasure;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Abstractions;

/// <summary>The Core module's data, implemented by Infrastructure.</summary>
public interface ICoreDbContext
{
    DbSet<Company> Companies { get; }

    DbSet<BusinessPartner> BusinessPartners { get; }

    DbSet<Article> Articles { get; }

    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }

    /// <summary>Queues the event in Core's outbox; it is saved by the next <see cref="SaveChangesAsync"/>.</summary>
    void AddIntegrationEvent(IIntegrationEvent integrationEvent);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
