using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Contracts.UnitsOfMeasure;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.UnitsOfMeasure;

/// <summary>All units of a company, ordered by code. Companies have few units, so this is not paged.</summary>
public sealed record ListUnitsOfMeasure(CompanyId CompanyId);

public sealed class ListUnitsOfMeasureHandler(ICoreDbContext db)
{
    public async Task<Result<IReadOnlyList<UnitOfMeasureResponse>>> HandleAsync(
        ListUnitsOfMeasure query,
        CancellationToken cancellationToken)
    {
        if (!await db.CompanyExistsAsync(query.CompanyId, cancellationToken))
        {
            return CoreErrors.CompanyNotFound;
        }

        var units = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.CompanyId == query.CompanyId)
            .OrderBy(u => u.Code)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<UnitOfMeasureResponse>>(
            units.Select(u => new UnitOfMeasureResponse(u.Id, u.Code, u.Name)).ToList());
    }
}
