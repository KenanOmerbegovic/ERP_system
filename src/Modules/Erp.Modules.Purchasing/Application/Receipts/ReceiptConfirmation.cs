using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Numbering;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Receipts;

/// <summary>
/// Moves a receipt that is being confirmed (or a storno being confirmed) into the warehouse and completes it. There is
/// no transaction across the two modules: the receipt was frozen as Confirming in an earlier transaction, the
/// Warehouse command is idempotent per receipt, and completion is a later transaction. If anything stops in between,
/// confirming the receipt again repeats the Warehouse call, which returns the same stock document, so stock moves once.
/// </summary>
internal sealed class ReceiptConfirmation(IPurchasingDbContext db, IPurchasingLocks locks, IStockDocumentCommands warehouse, IClock clock)
{
    public const string SourceModule = "purchasing";

    public static IdempotencyKey KeyOf(Guid receiptId) => new(SourceModule, receiptId);

    /// <summary>Steps 2 and 3 for a receipt in the Confirming state (loaded untracked by the caller).</summary>
    public async Task<Result> CompleteAsync(CompanyId companyId, Guid receiptId, CancellationToken cancellationToken)
    {
        db.ClearTracking();
        var receipt = await db.GoodsReceipts.AsNoTracking().Include(r => r.Lines).FirstAsync(r => r.Id == receiptId, cancellationToken);
        if (receipt.Status == DocumentStatus.Confirmed)
        {
            return Result.Success();
        }

        var stock = receipt.IsStorno
            ? await warehouse.ReverseAsync(new StockReversalCommand(KeyOf(receipt.Id), companyId, KeyOf(receipt.ReversesReceiptId!.Value)), cancellationToken)
            : await warehouse.ReceiveAsync(ToCommand(receipt), cancellationToken);
        return stock.IsFailure
            ? await UndoAsync(receipt, stock.Error, cancellationToken)
            : await FinishAsync(companyId, receipt.Id, stock.Value, cancellationToken);
    }

    private static StockReceiptCommand ToCommand(GoodsReceipt receipt) =>
        new(
            KeyOf(receipt.Id),
            receipt.CompanyId,
            receipt.WarehouseId,
            receipt.ReceiptDate,
            receipt.Note,
            receipt.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new StockReceiptCommandLine(l.ArticleId, l.Quantity, l.UnitCost, l.LotNumber, l.LotExpiryDate, l.SerialNumbers))
                .ToList());

    /// <summary>Step 3: number the receipt, record the stock document and line values, update the order, publish.</summary>
    private async Task<Result> FinishAsync(CompanyId companyId, Guid receiptId, StockDocumentReference stock, CancellationToken cancellationToken)
    {
        db.ClearTracking();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await locks.LockReceiptAsync(companyId, receiptId, cancellationToken);
        var receipt = await db.GoodsReceipts.Include(r => r.Lines).FirstAsync(r => r.Id == receiptId, cancellationToken);
        if (receipt.Status == DocumentStatus.Confirmed)
        {
            return Result.Success(); // Completed by a concurrent request.
        }

        await locks.LockOrderAsync(companyId, receipt.PurchaseOrderId, cancellationToken);
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.Id == receipt.PurchaseOrderId, cancellationToken);
        var number = await DocumentNumbers.NextAsync(db, locks, companyId, PurchasingDocumentType.GoodsReceipt, receipt.ReceiptDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error; // Stays Confirming; confirming again completes it once a series exists.
        }

        receipt.CompleteConfirmation(number.Value, stock.DocumentId, stock.Number, stock.LineValues, clock.UtcNow);
        var sign = receipt.IsStorno ? -1m : 1m;
        foreach (var line in receipt.Lines)
        {
            order.RecordReceived(line.OrderLineId, sign * line.Quantity);
        }

        db.AddIntegrationEvent(PurchasingEvents.Received(receipt, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>The warehouse refused: a receipt becomes a draft again; a storno is removed and its receipt is no longer reversed.</summary>
    private async Task<Result> UndoAsync(GoodsReceipt frozen, Error error, CancellationToken cancellationToken)
    {
        db.ClearTracking();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await locks.LockReceiptAsync(frozen.CompanyId, frozen.Id, cancellationToken);
        var receipt = await db.GoodsReceipts.Include(r => r.Lines).FirstAsync(r => r.Id == frozen.Id, cancellationToken);
        if (receipt.Status != DocumentStatus.Confirming)
        {
            return error;
        }

        if (receipt.IsStorno)
        {
            await locks.LockReceiptAsync(frozen.CompanyId, receipt.ReversesReceiptId!.Value, cancellationToken);
            var original = await db.GoodsReceipts.FirstAsync(r => r.Id == receipt.ReversesReceiptId, cancellationToken);
            original.CancelReversal(receipt.Id);
            db.GoodsReceipts.Remove(receipt);
        }
        else
        {
            receipt.AbortConfirmation();
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return error;
    }
}
