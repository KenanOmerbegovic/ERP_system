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
/// Confirms a goods receipt (FR-PUR-01): the goods enter the warehouse at the order price through the Warehouse command
/// contract, the receipt is numbered and the order's received quantities grow. If the warehouse refuses (e.g. a serial
/// number already in stock), the receipt is a draft again and the error is returned. Confirming a receipt left in the
/// Confirming state completes it; the warehouse moves the stock only once.
/// </summary>
public sealed record ConfirmGoodsReceipt(CompanyId CompanyId, Guid ReceiptId);

public sealed class ConfirmGoodsReceiptHandler(IPurchasingDbContext db, IPurchasingLocks locks, IStockDocumentCommands warehouse, IClock clock)
{
    public async Task<Result> HandleAsync(ConfirmGoodsReceipt command, CancellationToken cancellationToken)
    {
        var frozen = await FreezeAsync(command, cancellationToken);
        if (frozen.IsFailure)
        {
            return frozen;
        }

        return await new ReceiptConfirmation(db, locks, warehouse, clock).CompleteAsync(command.CompanyId, command.ReceiptId, cancellationToken);
    }

    /// <summary>Step 1: check the draft against the order and freeze it, so its quantities count as pending.</summary>
    private async Task<Result> FreezeAsync(ConfirmGoodsReceipt command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockReceiptAsync(command.CompanyId, command.ReceiptId, cancellationToken))
        {
            return PurchasingErrors.ReceiptNotFound;
        }

        var receipt = await db.GoodsReceipts.Include(r => r.Lines).FirstAsync(r => r.Id == command.ReceiptId, cancellationToken);
        switch (receipt.Status)
        {
            case DocumentStatus.Confirmed:
                return PurchasingErrors.ReceiptNotDraft;
            case DocumentStatus.Confirming:
                return Result.Success(); // A retry: go on to the warehouse.
        }

        await locks.LockOrderAsync(command.CompanyId, receipt.PurchaseOrderId, cancellationToken);
        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, receipt.PurchaseOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        var draft = new UpdateGoodsReceipt(
            receipt.CompanyId, receipt.Id, receipt.PurchaseOrderId, receipt.WarehouseId, receipt.ReceiptDate, receipt.Note,
            receipt.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new Domain.Receipts.GoodsReceiptLineInput(l.OrderLineId, l.Quantity, l.LotNumber, l.LotExpiryDate, l.SerialNumbers))
                .ToList());
        if (await GoodsReceiptRules.CheckAsync(db, order.Value, receipt.Id, draft, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        if (!await DocumentNumbers.ConfiguredAsync(db, command.CompanyId, PurchasingDocumentType.GoodsReceipt, receipt.ReceiptDate, cancellationToken))
        {
            return PurchasingErrors.NumberSeriesNotConfigured;
        }

        receipt.BeginConfirmation();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
