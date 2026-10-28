using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Core.Domain.BusinessPartners;
using Erp.Modules.Core.Domain.Companies;
using Erp.Modules.Core.Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Abstractions;

/// <summary>The Core module's data, implemented by Infrastructure.</summary>
public interface ICoreDbContext
{
    DbSet<Company> Companies { get; }

    DbSet<BusinessPartner> BusinessPartners { get; }

    DbSet<Article> Articles { get; }

    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
