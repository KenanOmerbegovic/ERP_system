using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Contracts.Warehouses;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.Storehouses;

/// <summary>All of the company's warehouses, active and inactive, ordered by code.</summary>
public sealed record ListWarehouses(CompanyId CompanyId);

public sealed class ListWarehousesHandler(IWarehouseDbContext db)
{
    public async Task<IReadOnlyList<WarehouseResponse>> HandleAsync(ListWarehouses query, CancellationToken cancellationToken) =>
        await db.Storehouses.AsNoTracking()
            .Where(s => s.CompanyId == query.CompanyId)
            .OrderBy(s => s.Code)
            .Select(s => new WarehouseResponse(s.Id, s.Code, s.Name, s.IsActive))
            .ToListAsync(cancellationToken);
}
