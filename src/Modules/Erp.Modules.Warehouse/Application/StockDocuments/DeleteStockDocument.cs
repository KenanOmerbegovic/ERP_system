using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.StockDocuments;

/// <summary>Deletes a draft. A confirmed document is never deleted; it is reversed by a storno.</summary>
public sealed record DeleteStockDocument(CompanyId CompanyId, Guid DocumentId);

public sealed class DeleteStockDocumentHandler(IWarehouseDbContext db, IStockLocks locks)
{
    public async Task<Result> HandleAsync(DeleteStockDocument command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockDocumentAsync(command.CompanyId, command.DocumentId, cancellationToken))
        {
            return WarehouseErrors.StockDocumentNotFound;
        }

        var document = await db.StockDocuments
            .Include(d => d.Lines)
            .FirstAsync(d => d.CompanyId == command.CompanyId && d.Id == command.DocumentId, cancellationToken);

        if (!document.IsDraft)
        {
            return WarehouseErrors.StockDocumentNotDraft;
        }

        db.StockDocuments.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
