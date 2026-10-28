using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Contracts.Companies;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Companies;

public sealed record ListCompanies;

public sealed class ListCompaniesHandler(ICoreDbContext db)
{
    public async Task<IReadOnlyList<CompanyResponse>> HandleAsync(ListCompanies query, CancellationToken cancellationToken)
    {
        var companies = await db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return companies
            .Select(c => new CompanyResponse(c.Id.Value, c.Name, c.TaxId, c.VatNumber, c.Address, c.PostalCode, c.City))
            .ToList();
    }
}
