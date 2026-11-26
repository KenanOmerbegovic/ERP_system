using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Deliveries;

/// <summary>
/// Reverses a confirmed, not invoiced delivery note (FR-SAL-05): a storno note, dated today, brings the goods back into
/// the warehouse at the cost they left at (Warehouse's reversal of the issue) and lowers the order's delivered
/// quantities. An invoiced note is corrected by a credit note (or by reversing the invoice first). Returns the storno's id.
/// </summary>
public sealed record StornoDeliveryNote(CompanyId CompanyId, Guid DeliveryNoteId);

public sealed class StornoDeliveryNoteHandler(ISalesDbContext db, ISalesLocks locks, IStockDocumentCommands warehouse, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(StornoDeliveryNote command, CancellationToken cancellationToken)
    {
        var storno = await BeginAsync(command, cancellationToken);
        if (storno.IsFailure)
        {
            return storno.Error;
        }

        var completed = await new DeliveryConfirmation(db, locks, warehouse, clock).CompleteAsync(command.CompanyId, storno.Value, cancellationToken);
        return completed.IsSuccess ? storno.Value : completed.Error;
    }

    /// <summary>Step 1: create the storno in the Confirming state, or find the one an interrupted storno left behind.</summary>
    private async Task<Result<Guid>> BeginAsync(StornoDeliveryNote command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockDeliveryNoteAsync(command.CompanyId, command.DeliveryNoteId, cancellationToken))
        {
            return SalesErrors.DeliveryNoteNotFound;
        }

        var note = await db.DeliveryNotes.Include(d => d.Lines).FirstAsync(d => d.Id == command.DeliveryNoteId, cancellationToken);
        if (note.Status != DocumentStatus.Confirmed)
        {
            return SalesErrors.DeliveryNoteNotConfirmed;
        }

        if (note.IsStorno)
        {
            return SalesErrors.IsStorno;
        }

        if (note.ReversedByDeliveryNoteId is { } existing)
        {
            var pending = await db.DeliveryNotes.AsNoTracking().AnyAsync(d => d.Id == existing && d.Status == DocumentStatus.Confirming, cancellationToken);
            return pending ? existing : SalesErrors.AlreadyReversed;
        }

        await locks.LockOrderAsync(command.CompanyId, note.SalesOrderId, cancellationToken);
        if (!note.CanBeReversed)
        {
            return SalesErrors.DeliveryNoteInvoiced;
        }

        var storno = note.CreateStorno(clock.Today, clock.UtcNow);
        if (!await DocumentNumbers.ConfiguredAsync(db, command.CompanyId, SalesDocumentType.DeliveryNote, storno.DeliveryDate, cancellationToken))
        {
            return SalesErrors.NumberSeriesNotConfigured;
        }

        db.DeliveryNotes.Add(storno);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return storno.Id;
    }
}
