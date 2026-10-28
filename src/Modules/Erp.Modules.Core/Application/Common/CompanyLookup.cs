using Erp.Modules.Core.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Common;

internal static class CompanyLookup
{
    public static Task<bool> CompanyExistsAsync(this ICoreDbContext db, CompanyId companyId, CancellationToken cancellationToken) =>
        db.Companies.AnyAsync(company => company.Id == companyId, cancellationToken);
}
