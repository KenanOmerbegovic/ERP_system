using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Contracts.Numbering;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Numbering;

public sealed record ListPurchasingNumberSeries(CompanyId CompanyId);

public sealed class ListPurchasingNumberSeriesHandler(IPurchasingDbContext db)
{
    public async Task<IReadOnlyList<PurchasingNumberSeriesResponse>> HandleAsync(ListPurchasingNumberSeries query, CancellationToken cancellationToken)
    {
        var series = await db.NumberSeries.AsNoTracking()
            .Where(s => s.CompanyId == query.CompanyId)
            .OrderBy(s => s.DocumentType)
            .ThenBy(s => s.ValidFrom)
            .ToListAsync(cancellationToken);
        return series.Select(s => new PurchasingNumberSeriesResponse(s.Id, s.DocumentType.ToKind(), s.ValidFrom, s.Prefix, s.Padding)).ToList();
    }
}
