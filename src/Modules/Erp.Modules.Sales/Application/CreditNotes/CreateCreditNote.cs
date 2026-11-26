using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Sales.Application.CreditNotes;

/// <summary>Saves a draft credit note against a confirmed invoice, with its PDV computed (FR-SAL-05).</summary>
public sealed record CreateCreditNote(
    CompanyId CompanyId,
    Guid InvoiceId,
    DateOnly CreditNoteDate,
    Guid? WarehouseId,
    string? Note,
    IReadOnlyList<CreditNoteDraftLine> Lines) : ICreditNoteDraft;

internal sealed class CreateCreditNoteValidator : AbstractValidator<CreateCreditNote>
{
    public CreateCreditNoteValidator(IClock clock)
    {
        Include(new CreditNoteDraftValidator(clock));
    }
}

public sealed class CreateCreditNoteHandler(ISalesDbContext db, IVatCalculator vatCalculator, IValidator<CreateCreditNote> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateCreditNote command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var invoice = await InvoiceLookup.OpenInvoiceAsync(db, command.CompanyId, command.InvoiceId, tracked: false, cancellationToken);
        if (invoice.IsFailure)
        {
            return invoice.Error;
        }

        var lines = await CreditNoteRules.ResolveAsync(db, invoice.Value, null, command, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        var creditNote = CreditNote.CreateDraft(
            command.CompanyId, invoice.Value.Id, invoice.Value.SalesOrderId, invoice.Value.CustomerId, invoice.Value.InvoiceDate, command.CreditNoteDate,
            command.WarehouseId, command.Note, lines.Value, clock.UtcNow);
        var vat = await CreditNoteRules.ApplyVatAsync(vatCalculator, creditNote, cancellationToken);
        if (vat.IsFailure)
        {
            return vat.Error;
        }

        db.CreditNotes.Add(creditNote);
        await db.SaveChangesAsync(cancellationToken);
        return creditNote.Id;
    }
}
