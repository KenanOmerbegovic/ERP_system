using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Receipts;
using Erp.Modules.Purchasing.Domain.Invoices;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Purchasing.Application.Invoices;

/// <summary>Saves a draft supplier invoice against an order's receipts, with its PDV computed (FR-PUR-02).</summary>
public sealed record CreateSupplierInvoice(
    CompanyId CompanyId,
    Guid PurchaseOrderId,
    string SupplierInvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string? Note,
    IReadOnlyList<SupplierInvoiceDraftLine> Lines) : ISupplierInvoiceDraft;

internal sealed class CreateSupplierInvoiceValidator : AbstractValidator<CreateSupplierInvoice>
{
    public CreateSupplierInvoiceValidator(IClock clock)
    {
        Include(new SupplierInvoiceDraftValidator(clock));
    }
}

public sealed class CreateSupplierInvoiceHandler(
    IPurchasingDbContext db, IVatCalculator vatCalculator, IValidator<CreateSupplierInvoice> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateSupplierInvoice command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, command.PurchaseOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        var lines = await SupplierInvoiceRules.ResolveAsync(db, order.Value, null, command, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        var invoice = SupplierInvoice.CreateDraft(
            command.CompanyId, order.Value.Id, order.Value.SupplierId, command.SupplierInvoiceNumber, command.InvoiceDate, command.DueDate,
            command.Note, lines.Value, clock.UtcNow);
        var vat = await SupplierInvoiceRules.ApplyVatAsync(vatCalculator, invoice, cancellationToken);
        if (vat.IsFailure)
        {
            return vat.Error;
        }

        db.SupplierInvoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }
}
