using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Receipts;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Invoices;

/// <summary>Replaces a draft invoice's content and recomputes its PDV. A registered invoice cannot be changed.</summary>
public sealed record UpdateSupplierInvoice(
    CompanyId CompanyId,
    Guid InvoiceId,
    Guid PurchaseOrderId,
    string SupplierInvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string? Note,
    IReadOnlyList<SupplierInvoiceDraftLine> Lines) : ISupplierInvoiceDraft;

internal sealed class UpdateSupplierInvoiceValidator : AbstractValidator<UpdateSupplierInvoice>
{
    public UpdateSupplierInvoiceValidator(IClock clock)
    {
        Include(new SupplierInvoiceDraftValidator(clock));
    }
}

public sealed class UpdateSupplierInvoiceHandler(
    IPurchasingDbContext db, IPurchasingLocks locks, IVatCalculator vatCalculator, IValidator<UpdateSupplierInvoice> validator)
{
    public async Task<Result> HandleAsync(UpdateSupplierInvoice command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockInvoiceAsync(command.CompanyId, command.InvoiceId, cancellationToken))
        {
            return PurchasingErrors.InvoiceNotFound;
        }

        var invoice = await db.SupplierInvoices.Include(i => i.Lines).Include(i => i.VatTotals).FirstAsync(i => i.Id == command.InvoiceId, cancellationToken);
        if (!invoice.IsDraft)
        {
            return PurchasingErrors.InvoiceNotDraft;
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (command.PurchaseOrderId != invoice.PurchaseOrderId)
        {
            var errors = new ValidationErrors();
            errors.Add(nameof(command.PurchaseOrderId), "SupplierInvoice.OrderCannotChange");
            return errors.ToError();
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, command.PurchaseOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        var lines = await SupplierInvoiceRules.ResolveAsync(db, order.Value, invoice.Id, command, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        invoice.UpdateDraft(command.SupplierInvoiceNumber, command.InvoiceDate, command.DueDate, command.Note, lines.Value);
        var vat = await SupplierInvoiceRules.ApplyVatAsync(vatCalculator, invoice, cancellationToken);
        if (vat.IsFailure)
        {
            return vat;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
