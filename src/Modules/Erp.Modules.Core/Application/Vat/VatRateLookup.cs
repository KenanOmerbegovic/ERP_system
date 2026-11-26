using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Contracts.Vat;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Vat;

/// <summary>Core's answer to <see cref="IVatRateLookup"/>: the latest rate of a code on or before a date.</summary>
internal sealed class VatRateLookup(ICoreDbContext db) : IVatRateLookup
{
    public async Task<VatRateInfo?> FindAsync(CompanyId companyId, string code, DateOnly date, CancellationToken cancellationToken)
    {
        var trimmed = code.Trim();
        return await db.VatRates.AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.Code == trimmed && r.ValidFrom <= date)
            .OrderByDescending(r => r.ValidFrom)
            .Select(r => new VatRateInfo(r.Code, r.Name, r.Rate, r.ValidFrom))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
