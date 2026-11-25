using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.StockDocuments;

/// <summary>
/// Reverses a confirmed document (NFR-INT-01): creates and confirms, in one step, a storno of the same type dated
/// today, with its own number, that applies the opposite movements at the original values. Returns the storno's id.
/// Reversing a receipt whose goods are already gone fails with Stock.Insufficient (FR-WH-03).
/// </summary>
public sealed record StornoStockDocument(CompanyId CompanyId, Guid DocumentId);

public sealed class StornoStockDocumentHandler(IWarehouseDbContext db, IStockLocks locks, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(StornoStockDocument command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockDocumentAsync(command.CompanyId, command.DocumentId, cancellationToken))
        {
            return WarehouseErrors.StockDocumentNotFound;
        }

        var original = await db.StockDocuments
            .Include(d => d.Lines)
            .FirstAsync(d => d.CompanyId == command.CompanyId && d.Id == command.DocumentId, cancellationToken);
        if (original.IsDraft)
        {
            return WarehouseErrors.StockDocumentNotConfirmed;
        }

        if (original.IsStorno)
        {
            return WarehouseErrors.StockDocumentIsStorno;
        }

        if (original.ReversedByDocumentId is not null)
        {
            return WarehouseErrors.StockDocumentAlreadyReversed;
        }

        var storno = original.CreateStorno(clock.Today, clock.UtcNow);
        db.StockDocuments.Add(storno);
        var result = await StockDocumentPosting.ConfirmAsync(db, locks, clock, storno, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await transaction.CommitAsync(cancellationToken);
        return storno.Id;
    }
}
