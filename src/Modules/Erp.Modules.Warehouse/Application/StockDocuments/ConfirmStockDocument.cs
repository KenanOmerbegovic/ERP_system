using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.StockDocuments;

/// <summary>
/// Confirms a draft: moves the stock, numbers the document and makes it immutable (FR-WH-01, NFR-INT-01).
/// Fails with Stock.Insufficient, saving nothing, if it would take more than is in stock (FR-WH-03); concurrent
/// confirmations of the same stock run one after another behind row locks, so they can never overdraw it.
/// </summary>
public sealed record ConfirmStockDocument(CompanyId CompanyId, Guid DocumentId);

public sealed class ConfirmStockDocumentHandler(IWarehouseDbContext db, IStockLocks locks, IClock clock)
{
    public async Task<Result> HandleAsync(ConfirmStockDocument command, CancellationToken cancellationToken)
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

        var result = await StockDocumentPosting.ConfirmAsync(db, locks, clock, document, cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
