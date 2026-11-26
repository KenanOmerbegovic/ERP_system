using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Deliveries;

/// <summary>
/// Confirms a delivery note (FR-SAL-03): the goods leave the warehouse through the Warehouse command contract, at the
/// weighted average cost, the note is numbered and the order's delivered quantities grow; GoodsDelivered carries the
/// cost of goods sold. If stock is insufficient (FR-WH-03) or the warehouse refuses otherwise, the note is a draft
/// again and the error, with the shortages, is returned. Confirming a note left in the Confirming state completes it;
/// the warehouse moves the stock only once.
/// </summary>
public sealed record ConfirmDeliveryNote(CompanyId CompanyId, Guid DeliveryNoteId);

public sealed class ConfirmDeliveryNoteHandler(ISalesDbContext db, ISalesLocks locks, IStockDocumentCommands warehouse, IClock clock)
{
    public async Task<Result> HandleAsync(ConfirmDeliveryNote command, CancellationToken cancellationToken)
    {
        var frozen = await FreezeAsync(command, cancellationToken);
        if (frozen.IsFailure)
        {
            return frozen;
        }

        return await new DeliveryConfirmation(db, locks, warehouse, clock).CompleteAsync(command.CompanyId, command.DeliveryNoteId, cancellationToken);
    }

    /// <summary>Step 1: check the draft against the order and freeze it, so its quantities count as pending.</summary>
    private async Task<Result> FreezeAsync(ConfirmDeliveryNote command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockDeliveryNoteAsync(command.CompanyId, command.DeliveryNoteId, cancellationToken))
        {
            return SalesErrors.DeliveryNoteNotFound;
        }

        var note = await db.DeliveryNotes.Include(d => d.Lines).FirstAsync(d => d.Id == command.DeliveryNoteId, cancellationToken);
        switch (note.Status)
        {
            case DocumentStatus.Confirmed:
                return SalesErrors.DeliveryNoteNotDraft;
            case DocumentStatus.Confirming:
                return Result.Success(); // A retry: go on to the warehouse.
        }

        await locks.LockOrderAsync(command.CompanyId, note.SalesOrderId, cancellationToken);
        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, note.SalesOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        var draft = new UpdateDeliveryNote(
            note.CompanyId, note.Id, note.SalesOrderId, note.WarehouseId, note.DeliveryDate, note.Note,
            note.Lines.OrderBy(l => l.LineNumber).Select(l => new DeliveryNoteLineInput(l.OrderLineId, l.Quantity, l.LotNumber, l.SerialNumbers)).ToList());
        if (await DeliveryNoteRules.CheckAsync(db, order.Value, note.Id, draft, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        if (!await DocumentNumbers.ConfiguredAsync(db, command.CompanyId, SalesDocumentType.DeliveryNote, note.DeliveryDate, cancellationToken))
        {
            return SalesErrors.NumberSeriesNotConfigured;
        }

        note.BeginConfirmation();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
