using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
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
/// Confirms a credit note (FR-SAL-05, NFR-INT-01): re-checks it against the invoice under the invoice's lock, recomputes
/// its PDV, numbers it, credits the invoice lines and publishes CreditNoteIssued. Returned goods come back into the
/// warehouse through the Warehouse command contract first (at the delivery's cost); if the warehouse refuses, the credit
/// note is a draft again. Confirming one left in the Confirming state completes it; stock moves only once.
/// </summary>
public sealed record ConfirmCreditNote(CompanyId CompanyId, Guid CreditNoteId);

public sealed class ConfirmCreditNoteHandler(
    ISalesDbContext db, ISalesLocks locks, IVatCalculator vatCalculator, IStockDocumentCommands warehouse, IClock clock)
{
    public async Task<Result> HandleAsync(ConfirmCreditNote command, CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(command, cancellationToken);
        if (prepared.IsFailure || !prepared.Value)
        {
            return prepared.IsFailure ? prepared.Error : Result.Success();
        }

        return await new CreditNoteConfirmation(db, locks, warehouse, clock).CompleteAsync(command.CompanyId, command.CreditNoteId, cancellationToken);
    }

    /// <summary>
    /// Checks the draft under the invoice's lock. Without returns it is confirmed here (result false: nothing more to do);
    /// with returns it is frozen as Confirming for the warehouse (result true).
    /// </summary>
    private async Task<Result<bool>> PrepareAsync(ConfirmCreditNote command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockCreditNoteAsync(command.CompanyId, command.CreditNoteId, cancellationToken))
        {
            return SalesErrors.CreditNoteNotFound;
        }

        var creditNote = await db.CreditNotes.Include(c => c.Lines).Include(c => c.VatTotals).AsSplitQuery()
            .FirstAsync(c => c.Id == command.CreditNoteId, cancellationToken);
        switch (creditNote.Status)
        {
            case DocumentStatus.Confirmed:
                return SalesErrors.CreditNoteNotDraft;
            case DocumentStatus.Confirming:
                return true; // A retry: go on to the warehouse.
        }

        await locks.LockInvoiceAsync(command.CompanyId, creditNote.InvoiceId, cancellationToken);
        var invoice = await InvoiceLookup.OpenInvoiceAsync(db, command.CompanyId, creditNote.InvoiceId, tracked: true, cancellationToken);
        if (invoice.IsFailure)
        {
            return invoice.Error;
        }

        var draft = new UpdateCreditNote(
            creditNote.CompanyId, creditNote.Id, creditNote.InvoiceId, creditNote.CreditNoteDate, creditNote.WarehouseId, creditNote.Note,
            creditNote.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new CreditNoteDraftLine(
                    l.InvoiceLineId, l.Kind, l.Kind == CreditKind.Return ? l.Quantity : null, l.Kind == CreditKind.ValueReduction ? l.NetAmount : null,
                    l.SerialNumbers))
                .ToList());
        var lines = await CreditNoteRules.ResolveAsync(db, invoice.Value, creditNote.Id, draft, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        creditNote.UpdateDraft(draft.CreditNoteDate, draft.WarehouseId, draft.Note, lines.Value);
        var vat = await CreditNoteRules.ApplyVatAsync(vatCalculator, creditNote, cancellationToken);
        if (vat.IsFailure)
        {
            return vat.Error;
        }

        if (creditNote.HasReturns)
        {
            if (!await DocumentNumbers.ConfiguredAsync(db, command.CompanyId, SalesDocumentType.CreditNote, creditNote.CreditNoteDate, cancellationToken))
            {
                return SalesErrors.NumberSeriesNotConfigured;
            }

            creditNote.BeginConfirmation();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        var number = await DocumentNumbers.NextAsync(db, locks, command.CompanyId, SalesDocumentType.CreditNote, creditNote.CreditNoteDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error;
        }

        creditNote.Confirm(number.Value, clock.UtcNow);
        CreditedAmounts.Apply(invoice.Value, creditNote);
        db.AddIntegrationEvent(SalesEvents.CreditNoteIssued(creditNote, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return false;
    }
}
