using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Contracts.Vat;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Vat;

/// <summary>Every rate of the company, by code and start date (the history included).</summary>
public sealed record ListVatRates(CompanyId CompanyId);

public sealed class ListVatRatesHandler(ICoreDbContext db)
{
    public async Task<IReadOnlyList<VatRateResponse>> HandleAsync(ListVatRates query, CancellationToken cancellationToken) =>
        await db.VatRates.AsNoTracking()
            .Where(r => r.CompanyId == query.CompanyId)
            .OrderBy(r => r.Code)
            .ThenBy(r => r.ValidFrom)
            .Select(r => new VatRateResponse(r.Id, r.Code, r.Name, r.Rate, r.ValidFrom))
            .ToListAsync(cancellationToken);
}
