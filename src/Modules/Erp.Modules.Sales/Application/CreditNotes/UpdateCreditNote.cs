using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.CreditNotes;

/// <summary>Replaces a draft credit note's content and recomputes its PDV; it stays with its invoice.</summary>
public sealed record UpdateCreditNote(
    CompanyId CompanyId,
    Guid CreditNoteId,
    Guid InvoiceId,
    DateOnly CreditNoteDate,
    Guid? WarehouseId,
    string? Note,
    IReadOnlyList<CreditNoteDraftLine> Lines) : ICreditNoteDraft;

internal sealed class UpdateCreditNoteValidator : AbstractValidator<UpdateCreditNote>
{
    public UpdateCreditNoteValidator(IClock clock)
    {
        Include(new CreditNoteDraftValidator(clock));
    }
}

public sealed class UpdateCreditNoteHandler(ISalesDbContext db, ISalesLocks locks, IVatCalculator vatCalculator, IValidator<UpdateCreditNote> validator)
{
    public async Task<Result> HandleAsync(UpdateCreditNote command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockCreditNoteAsync(command.CompanyId, command.CreditNoteId, cancellationToken))
        {
            return SalesErrors.CreditNoteNotFound;
        }

        var creditNote = await db.CreditNotes.Include(c => c.Lines).Include(c => c.VatTotals).AsSplitQuery()
            .FirstAsync(c => c.Id == command.CreditNoteId, cancellationToken);
        if (!creditNote.IsDraft)
        {
            return SalesErrors.CreditNoteNotDraft;
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (command.InvoiceId != creditNote.InvoiceId)
        {
            var errors = new ValidationErrors();
            errors.Add(nameof(command.InvoiceId), "CreditNote.InvoiceFixed");
            return errors.ToError();
        }

        var invoice = await InvoiceLookup.OpenInvoiceAsync(db, command.CompanyId, creditNote.InvoiceId, tracked: false, cancellationToken);
        if (invoice.IsFailure)
        {
            return invoice.Error;
        }

        var lines = await CreditNoteRules.ResolveAsync(db, invoice.Value, creditNote.Id, command, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        creditNote.UpdateDraft(command.CreditNoteDate, command.WarehouseId, command.Note, lines.Value);
        var vat = await CreditNoteRules.ApplyVatAsync(vatCalculator, creditNote, cancellationToken);
        if (vat.IsFailure)
        {
            return vat;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
