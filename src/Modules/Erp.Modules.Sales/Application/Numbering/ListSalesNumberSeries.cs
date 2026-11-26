using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Numbering;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Numbering;

public sealed record ListSalesNumberSeries(CompanyId CompanyId);

public sealed class ListSalesNumberSeriesHandler(ISalesDbContext db)
{
    public async Task<IReadOnlyList<SalesNumberSeriesResponse>> HandleAsync(ListSalesNumberSeries query, CancellationToken cancellationToken)
    {
        var series = await db.NumberSeries.AsNoTracking()
            .Where(s => s.CompanyId == query.CompanyId)
            .OrderBy(s => s.DocumentType)
            .ThenBy(s => s.ValidFrom)
            .ToListAsync(cancellationToken);
        return series.Select(s => new SalesNumberSeriesResponse(s.Id, s.DocumentType.ToKind(), s.ValidFrom, s.Prefix, s.Padding)).ToList();
    }
}
