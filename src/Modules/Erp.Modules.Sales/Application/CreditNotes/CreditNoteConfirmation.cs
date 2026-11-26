using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Deliveries;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.CreditNotes;

/// <summary>
/// Takes a credit note with returns that is being confirmed (or a storno of one) through the warehouse and completes
/// it, like <see cref="DeliveryConfirmation"/>: returned goods are received back at the delivery's cost (key: the credit
/// note), a storno takes them out again (Warehouse's reversal of that receipt). Credit notes without returns never get
/// here; they are confirmed in one transaction.
/// </summary>
internal sealed class CreditNoteConfirmation(ISalesDbContext db, ISalesLocks locks, IStockDocumentCommands warehouse, IClock clock)
{
    public static IdempotencyKey KeyOf(Guid creditNoteId) => new(DeliveryConfirmation.SourceModule, creditNoteId);

    public async Task<Result> CompleteAsync(CompanyId companyId, Guid creditNoteId, CancellationToken cancellationToken)
    {
        db.ClearTracking();
        var creditNote = await db.CreditNotes.AsNoTracking().Include(c => c.Lines).FirstAsync(c => c.Id == creditNoteId, cancellationToken);
        if (creditNote.Status == DocumentStatus.Confirmed)
        {
            return Result.Success();
        }

        var stock = creditNote.IsStorno
            ? await warehouse.ReverseAsync(new StockReversalCommand(KeyOf(creditNote.Id), companyId, KeyOf(creditNote.ReversesCreditNoteId!.Value)), cancellationToken)
            : await warehouse.ReceiveAsync(ToCommand(creditNote), cancellationToken);
        return stock.IsFailure
            ? await UndoAsync(creditNote, stock.Error, cancellationToken)
            : await FinishAsync(companyId, creditNote.Id, stock.Value, cancellationToken);
    }

    private static StockReceiptCommand ToCommand(CreditNote creditNote) =>
        new(
            KeyOf(creditNote.Id),
            creditNote.CompanyId,
            creditNote.WarehouseId!.Value,
            creditNote.CreditNoteDate,
            creditNote.Note,
            creditNote.ReturnLines
                .Select(l => new StockReceiptCommandLine(l.ArticleId, l.Quantity, l.ReturnUnitCost, l.LotNumber, null, l.SerialNumbers))
                .ToList());

    /// <summary>Number the credit note, record the warehouse receipt and returned values, credit the invoice, publish.</summary>
    private async Task<Result> FinishAsync(CompanyId companyId, Guid creditNoteId, StockDocumentReference stock, CancellationToken cancellationToken)
    {
        db.ClearTracking();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await locks.LockCreditNoteAsync(companyId, creditNoteId, cancellationToken);
        var creditNote = await db.CreditNotes.Include(c => c.Lines).Include(c => c.VatTotals).AsSplitQuery()
            .FirstAsync(c => c.Id == creditNoteId, cancellationToken);
        if (creditNote.Status == DocumentStatus.Confirmed)
        {
            return Result.Success(); // Completed by a concurrent request.
        }

        await locks.LockInvoiceAsync(companyId, creditNote.InvoiceId, cancellationToken);
        var invoice = await db.SalesInvoices.Include(i => i.Lines).FirstAsync(i => i.Id == creditNote.InvoiceId, cancellationToken);
        var number = await DocumentNumbers.NextAsync(db, locks, companyId, SalesDocumentType.CreditNote, creditNote.CreditNoteDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error; // Stays Confirming; confirming again completes it once a series exists.
        }

        creditNote.CompleteConfirmation(number.Value, stock.DocumentId, stock.Number, stock.LineValues, clock.UtcNow);
        CreditedAmounts.Apply(invoice, creditNote);
        db.AddIntegrationEvent(SalesEvents.CreditNoteIssued(creditNote, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> UndoAsync(CreditNote frozen, Error error, CancellationToken cancellationToken)
    {
        db.ClearTracking();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await locks.LockCreditNoteAsync(frozen.CompanyId, frozen.Id, cancellationToken);
        var creditNote = await db.CreditNotes.Include(c => c.Lines).Include(c => c.VatTotals).AsSplitQuery()
            .FirstAsync(c => c.Id == frozen.Id, cancellationToken);
        if (creditNote.Status != DocumentStatus.Confirming)
        {
            return error;
        }

        if (creditNote.IsStorno)
        {
            await locks.LockCreditNoteAsync(frozen.CompanyId, creditNote.ReversesCreditNoteId!.Value, cancellationToken);
            var original = await db.CreditNotes.FirstAsync(c => c.Id == creditNote.ReversesCreditNoteId, cancellationToken);
            original.CancelReversal(creditNote.Id);
            db.CreditNotes.Remove(creditNote);
        }
        else
        {
            creditNote.AbortConfirmation();
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return error;
    }
}
