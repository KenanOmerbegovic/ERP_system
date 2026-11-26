using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Numbering;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Receipts;

/// <summary>
/// Reverses a confirmed, not invoiced receipt (NFR-INT-01): a storno receipt, dated today, takes the goods back out of
/// the warehouse (Warehouse's reversal of the receipt's stock document) and lowers the order's received quantities.
/// Fails with Stock.Insufficient if the goods have left the warehouse. Returns the storno's id.
/// </summary>
public sealed record StornoGoodsReceipt(CompanyId CompanyId, Guid ReceiptId);

public sealed class StornoGoodsReceiptHandler(IPurchasingDbContext db, IPurchasingLocks locks, IStockDocumentCommands warehouse, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(StornoGoodsReceipt command, CancellationToken cancellationToken)
    {
        var storno = await BeginAsync(command, cancellationToken);
        if (storno.IsFailure)
        {
            return storno.Error;
        }

        var completed = await new ReceiptConfirmation(db, locks, warehouse, clock).CompleteAsync(command.CompanyId, storno.Value, cancellationToken);
        return completed.IsSuccess ? storno.Value : completed.Error;
    }

    /// <summary>Step 1: create the storno in the Confirming state, or find the one an interrupted storno left behind.</summary>
    private async Task<Result<Guid>> BeginAsync(StornoGoodsReceipt command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockReceiptAsync(command.CompanyId, command.ReceiptId, cancellationToken))
        {
            return PurchasingErrors.ReceiptNotFound;
        }

        var receipt = await db.GoodsReceipts.Include(r => r.Lines).FirstAsync(r => r.Id == command.ReceiptId, cancellationToken);
        if (receipt.Status != DocumentStatus.Confirmed)
        {
            return PurchasingErrors.ReceiptNotConfirmed;
        }

        if (receipt.IsStorno)
        {
            return PurchasingErrors.IsStorno;
        }

        if (receipt.ReversedByReceiptId is { } existing)
        {
            var pending = await db.GoodsReceipts.AsNoTracking().AnyAsync(r => r.Id == existing && r.Status == DocumentStatus.Confirming, cancellationToken);
            return pending ? existing : PurchasingErrors.AlreadyReversed;
        }

        await locks.LockOrderAsync(command.CompanyId, receipt.PurchaseOrderId, cancellationToken);
        if (!receipt.CanBeReversed)
        {
            return PurchasingErrors.ReceiptInvoiced;
        }

        var storno = receipt.CreateStorno(clock.Today, clock.UtcNow);
        if (!await DocumentNumbers.ConfiguredAsync(db, command.CompanyId, PurchasingDocumentType.GoodsReceipt, storno.ReceiptDate, cancellationToken))
        {
            return PurchasingErrors.NumberSeriesNotConfigured;
        }

        db.GoodsReceipts.Add(storno);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return storno.Id;
    }
}
