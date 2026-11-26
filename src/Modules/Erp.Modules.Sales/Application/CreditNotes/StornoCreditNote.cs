using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.CreditNotes;

/// <summary>
/// Reverses a confirmed credit note with a storno dated today (FR-SAL-05): the invoice is owed in full again, and
/// returned goods leave the warehouse again (Warehouse's reversal of the return; Stock.Insufficient if they are gone).
/// Returns the storno's id.
/// </summary>
public sealed record StornoCreditNote(CompanyId CompanyId, Guid CreditNoteId);

public sealed class StornoCreditNoteHandler(ISalesDbContext db, ISalesLocks locks, IStockDocumentCommands warehouse, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(StornoCreditNote command, CancellationToken cancellationToken)
    {
        var storno = await BeginAsync(command, cancellationToken);
        if (storno.IsFailure || !storno.Value.ViaWarehouse)
        {
            return storno.IsFailure ? storno.Error : storno.Value.Id;
        }

        var completed = await new CreditNoteConfirmation(db, locks, warehouse, clock).CompleteAsync(command.CompanyId, storno.Value.Id, cancellationToken);
        return completed.IsSuccess ? storno.Value.Id : completed.Error;
    }

    /// <summary>
    /// Creates the storno. Without returns it is confirmed here; with returns it is left Confirming for the warehouse
    /// (or the one an interrupted storno left behind is picked up).
    /// </summary>
    private async Task<Result<(Guid Id, bool ViaWarehouse)>> BeginAsync(StornoCreditNote command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockCreditNoteAsync(command.CompanyId, command.CreditNoteId, cancellationToken))
        {
            return SalesErrors.CreditNoteNotFound;
        }

        var creditNote = await db.CreditNotes.Include(c => c.Lines).Include(c => c.VatTotals).AsSplitQuery()
            .FirstAsync(c => c.Id == command.CreditNoteId, cancellationToken);
        if (creditNote.Status != DocumentStatus.Confirmed)
        {
            return SalesErrors.CreditNoteNotConfirmed;
        }

        if (creditNote.IsStorno)
        {
            return SalesErrors.IsStorno;
        }

        if (creditNote.ReversedByCreditNoteId is { } existing)
        {
            var pending = await db.CreditNotes.AsNoTracking().AnyAsync(c => c.Id == existing && c.Status == DocumentStatus.Confirming, cancellationToken);
            return pending ? (existing, true) : SalesErrors.AlreadyReversed;
        }

        await locks.LockInvoiceAsync(command.CompanyId, creditNote.InvoiceId, cancellationToken);
        var storno = creditNote.CreateStorno(clock.Today, clock.UtcNow);
        db.CreditNotes.Add(storno);
        if (storno.HasReturns)
        {
            if (!await DocumentNumbers.ConfiguredAsync(db, command.CompanyId, SalesDocumentType.CreditNote, storno.CreditNoteDate, cancellationToken))
            {
                return SalesErrors.NumberSeriesNotConfigured;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (storno.Id, true);
        }

        var number = await DocumentNumbers.NextAsync(db, locks, command.CompanyId, SalesDocumentType.CreditNote, storno.CreditNoteDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error;
        }

        var invoice = await db.SalesInvoices.Include(i => i.Lines).FirstAsync(i => i.Id == creditNote.InvoiceId, cancellationToken);
        storno.Confirm(number.Value, clock.UtcNow);
        CreditedAmounts.Apply(invoice, storno);
        db.AddIntegrationEvent(SalesEvents.CreditNoteIssued(storno, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (storno.Id, false);
    }
}
