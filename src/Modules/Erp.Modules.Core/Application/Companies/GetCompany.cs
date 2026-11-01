using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Contracts.Companies;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Companies;

/// <summary>
/// One company. There is no list of all companies: each company's data is private to it.
/// A user's companies come from GET /api/me (Identity).
/// </summary>
public sealed record GetCompany(CompanyId CompanyId);

public sealed class GetCompanyHandler(ICoreDbContext db)
{
    public async Task<Result<CompanyResponse>> HandleAsync(GetCompany query, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == query.CompanyId, cancellationToken);
        return company is null
            ? CoreErrors.CompanyNotFound
            : new CompanyResponse(company.Id.Value, company.Name, company.TaxId, company.VatNumber, company.Address, company.PostalCode, company.City);
    }
}
