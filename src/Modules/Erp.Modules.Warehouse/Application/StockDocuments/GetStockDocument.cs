using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.StockDocuments;

public sealed record GetStockDocument(CompanyId CompanyId, Guid DocumentId);

public sealed class GetStockDocumentHandler(IWarehouseDbContext db)
{
    public async Task<Result<StockDocumentResponse>> HandleAsync(GetStockDocument query, CancellationToken cancellationToken)
    {
        var document = await db.StockDocuments.AsNoTracking()
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.CompanyId == query.CompanyId && d.Id == query.DocumentId, cancellationToken);
        if (document is null)
        {
            return WarehouseErrors.StockDocumentNotFound;
        }

        var articleIds = document.Lines.Select(line => line.ArticleId).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking()
            .Where(a => a.CompanyId == query.CompanyId && articleIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var lines = document.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line => new StockDocumentLineResponse(
                line.Id,
                line.LineNumber,
                line.ArticleId,
                articles[line.ArticleId].Code,
                articles[line.ArticleId].Name,
                line.Quantity,
                line.UnitCost,
                line.Value,
                line.LotNumber,
                line.LotExpiryDate,
                line.SerialNumbers))
            .ToList();
        return new StockDocumentResponse(
            document.Id,
            document.Type.ToKind(),
            document.Status.ToState(),
            document.Number,
            document.DocumentDate,
            document.SourceStorehouseId,
            document.TargetStorehouseId,
            document.Note,
            document.ReversesDocumentId,
            document.ReversedByDocumentId,
            document.ConfirmedAt,
            document.TotalValue,
            lines,
            document.SourceModule,
            document.SourceDocumentId);
    }
}
