using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Numbering;
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
/// Takes a delivery note that is being confirmed (or a storno being confirmed) through the warehouse and completes it.
/// There is no transaction across the two modules: the note was frozen as Confirming in an earlier transaction, the
/// Warehouse command is idempotent per note, and completion is a later transaction. If anything stops in between,
/// confirming the note again repeats the Warehouse call, which returns the same stock document, so stock moves once.
/// </summary>
internal sealed class DeliveryConfirmation(ISalesDbContext db, ISalesLocks locks, IStockDocumentCommands warehouse, IClock clock)
{
    public const string SourceModule = "sales";

    public static IdempotencyKey KeyOf(Guid deliveryNoteId) => new(SourceModule, deliveryNoteId);

    /// <summary>Steps 2 and 3 for a note in the Confirming state.</summary>
    public async Task<Result> CompleteAsync(CompanyId companyId, Guid deliveryNoteId, CancellationToken cancellationToken)
    {
        db.ClearTracking();
        var note = await db.DeliveryNotes.AsNoTracking().Include(d => d.Lines).FirstAsync(d => d.Id == deliveryNoteId, cancellationToken);
        if (note.Status == DocumentStatus.Confirmed)
        {
            return Result.Success();
        }

        var stock = note.IsStorno
            ? await warehouse.ReverseAsync(new StockReversalCommand(KeyOf(note.Id), companyId, KeyOf(note.ReversesDeliveryNoteId!.Value)), cancellationToken)
            : await warehouse.IssueAsync(ToCommand(note), cancellationToken);
        return stock.IsFailure
            ? await UndoAsync(note, stock.Error, cancellationToken)
            : await FinishAsync(companyId, note.Id, stock.Value, cancellationToken);
    }

    private static StockIssueCommand ToCommand(DeliveryNote note) =>
        new(
            KeyOf(note.Id),
            note.CompanyId,
            note.WarehouseId,
            note.DeliveryDate,
            note.Note,
            note.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new StockIssueCommandLine(l.ArticleId, l.Quantity, l.LotNumber, l.SerialNumbers))
                .ToList());

    /// <summary>Step 3: number the note, record the warehouse issue and the cost of each line, update the order, publish.</summary>
    private async Task<Result> FinishAsync(CompanyId companyId, Guid deliveryNoteId, StockDocumentReference stock, CancellationToken cancellationToken)
    {
        db.ClearTracking();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await locks.LockDeliveryNoteAsync(companyId, deliveryNoteId, cancellationToken);
        var note = await db.DeliveryNotes.Include(d => d.Lines).FirstAsync(d => d.Id == deliveryNoteId, cancellationToken);
        if (note.Status == DocumentStatus.Confirmed)
        {
            return Result.Success(); // Completed by a concurrent request.
        }

        await locks.LockOrderAsync(companyId, note.SalesOrderId, cancellationToken);
        var order = await db.SalesOrders.Include(o => o.Lines).FirstAsync(o => o.Id == note.SalesOrderId, cancellationToken);
        var number = await DocumentNumbers.NextAsync(db, locks, companyId, SalesDocumentType.DeliveryNote, note.DeliveryDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error; // Stays Confirming; confirming again completes it once a series exists.
        }

        note.CompleteConfirmation(number.Value, stock.DocumentId, stock.Number, stock.LineValues, clock.UtcNow);
        var sign = note.IsStorno ? -1m : 1m;
        foreach (var line in note.Lines)
        {
            order.RecordDelivered(line.OrderLineId, sign * line.Quantity);
        }

        db.AddIntegrationEvent(SalesEvents.Delivered(note, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// The warehouse refused (e.g. Stock.Insufficient, FR-WH-03): a note becomes a draft again, so the user can see the
    /// shortages and change it; a storno is removed and its note is no longer reversed.
    /// </summary>
    private async Task<Result> UndoAsync(DeliveryNote frozen, Error error, CancellationToken cancellationToken)
    {
        db.ClearTracking();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await locks.LockDeliveryNoteAsync(frozen.CompanyId, frozen.Id, cancellationToken);
        var note = await db.DeliveryNotes.Include(d => d.Lines).FirstAsync(d => d.Id == frozen.Id, cancellationToken);
        if (note.Status != DocumentStatus.Confirming)
        {
            return error;
        }

        if (note.IsStorno)
        {
            await locks.LockDeliveryNoteAsync(frozen.CompanyId, note.ReversesDeliveryNoteId!.Value, cancellationToken);
            var original = await db.DeliveryNotes.FirstAsync(d => d.Id == note.ReversesDeliveryNoteId, cancellationToken);
            original.CancelReversal(note.Id);
            db.DeliveryNotes.Remove(note);
        }
        else
        {
            note.AbortConfirmation();
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return error;
    }
}
