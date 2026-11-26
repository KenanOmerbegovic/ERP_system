using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Numbering;
using Erp.Modules.Purchasing.Application.Receipts;
using Erp.Modules.Purchasing.Domain.Invoices;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Invoices;

/// <summary>
/// Registers a draft supplier invoice (FR-PUR-02): re-checks it against the receipts under the order's lock, recomputes
/// its PDV for the invoice date, numbers it, raises the invoiced quantities and publishes SupplierInvoiceRegistered with
/// the price differences for Finance.
/// </summary>
public sealed record ConfirmSupplierInvoice(CompanyId CompanyId, Guid InvoiceId);

public sealed class ConfirmSupplierInvoiceHandler(IPurchasingDbContext db, IPurchasingLocks locks, IVatCalculator vatCalculator, IClock clock)
{
    public async Task<Result> HandleAsync(ConfirmSupplierInvoice command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockInvoiceAsync(command.CompanyId, command.InvoiceId, cancellationToken))
        {
            return PurchasingErrors.InvoiceNotFound;
        }

        var invoiceOrderId = await db.SupplierInvoices.AsNoTracking().Where(i => i.Id == command.InvoiceId).Select(i => i.PurchaseOrderId).FirstAsync(cancellationToken);
        await locks.LockOrderAsync(command.CompanyId, invoiceOrderId, cancellationToken);
        var invoice = await db.SupplierInvoices.Include(i => i.Lines).Include(i => i.VatTotals).FirstAsync(i => i.Id == command.InvoiceId, cancellationToken);
        if (!invoice.IsDraft)
        {
            return PurchasingErrors.InvoiceNotDraft;
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, invoice.PurchaseOrderId, tracked: true, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        var draft = new UpdateSupplierInvoice(
            invoice.CompanyId, invoice.Id, invoice.PurchaseOrderId, invoice.SupplierInvoiceNumber, invoice.InvoiceDate, invoice.DueDate, invoice.Note,
            invoice.Lines.OrderBy(l => l.LineNumber).Select(l => new SupplierInvoiceDraftLine(l.ReceiptLineId, l.Quantity, l.UnitPrice, l.VatCode)).ToList());
        var lines = await SupplierInvoiceRules.ResolveAsync(db, order.Value, invoice.Id, draft, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        invoice.UpdateDraft(draft.SupplierInvoiceNumber, draft.InvoiceDate, draft.DueDate, draft.Note, lines.Value);
        var vat = await SupplierInvoiceRules.ApplyVatAsync(vatCalculator, invoice, cancellationToken);
        if (vat.IsFailure)
        {
            return vat;
        }

        var number = await DocumentNumbers.NextAsync(db, locks, command.CompanyId, PurchasingDocumentType.SupplierInvoice, invoice.InvoiceDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error;
        }

        invoice.Confirm(number.Value, clock.UtcNow);
        await InvoicedQuantities.ApplyAsync(db, order.Value, invoice, cancellationToken);
        db.AddIntegrationEvent(PurchasingEvents.InvoiceRegistered(invoice, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
