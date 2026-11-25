using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Contracts.Numbering;
using Erp.Modules.Warehouse.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.Numbering;

/// <summary>All series of the company, by document type and start date.</summary>
public sealed record ListNumberSeries(CompanyId CompanyId);

public sealed class ListNumberSeriesHandler(IWarehouseDbContext db)
{
    public async Task<IReadOnlyList<NumberSeriesResponse>> HandleAsync(ListNumberSeries query, CancellationToken cancellationToken)
    {
        var series = await db.NumberSeries.AsNoTracking()
            .Where(s => s.CompanyId == query.CompanyId)
            .OrderBy(s => s.DocumentType)
            .ThenBy(s => s.ValidFrom)
            .ToListAsync(cancellationToken);
        return series.Select(s => new NumberSeriesResponse(s.Id, s.DocumentType.ToKind(), s.ValidFrom, s.Prefix, s.Padding)).ToList();
    }
}
