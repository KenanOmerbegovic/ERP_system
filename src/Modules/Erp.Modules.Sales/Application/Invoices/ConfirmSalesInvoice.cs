using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Invoices;

/// <summary>
/// Confirms (issues) a draft invoice (FR-SAL-03, FR-SAL-04): re-checks it against the deliveries and the order under the
/// order's lock, recomputes its PDV for the invoice date, numbers it, raises the invoiced quantities and publishes
/// InvoiceIssued. Afterwards it is immutable (FR-SAL-05).
/// </summary>
public sealed record ConfirmSalesInvoice(CompanyId CompanyId, Guid InvoiceId);

public sealed class ConfirmSalesInvoiceHandler(ISalesDbContext db, ISalesLocks locks, IVatCalculator vatCalculator, IClock clock)
{
    public async Task<Result> HandleAsync(ConfirmSalesInvoice command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockInvoiceAsync(command.CompanyId, command.InvoiceId, cancellationToken))
        {
            return SalesErrors.InvoiceNotFound;
        }

        var orderId = await db.SalesInvoices.AsNoTracking().Where(i => i.Id == command.InvoiceId).Select(i => i.SalesOrderId).FirstAsync(cancellationToken);
        await locks.LockOrderAsync(command.CompanyId, orderId, cancellationToken);
        var invoice = await db.SalesInvoices.Include(i => i.Lines).Include(i => i.VatTotals).AsSplitQuery()
            .FirstAsync(i => i.Id == command.InvoiceId, cancellationToken);
        if (!invoice.IsDraft)
        {
            return SalesErrors.InvoiceNotDraft;
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, invoice.SalesOrderId, tracked: true, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        var draft = new UpdateSalesInvoice(
            invoice.CompanyId, invoice.Id, invoice.SalesOrderId, invoice.InvoiceDate, invoice.DueDate, invoice.Note,
            invoice.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new SalesInvoiceDraftLine(l.DeliveryLineId, l.IsService ? l.OrderLineId : null, l.Quantity))
                .ToList());
        var lines = await SalesInvoiceRules.ResolveAsync(db, order.Value, draft, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        invoice.UpdateDraft(draft.InvoiceDate, draft.DueDate, draft.Note, lines.Value);
        var vat = await SalesInvoiceRules.ApplyVatAsync(vatCalculator, invoice, cancellationToken);
        if (vat.IsFailure)
        {
            return vat;
        }

        var number = await DocumentNumbers.NextAsync(db, locks, command.CompanyId, SalesDocumentType.SalesInvoice, invoice.InvoiceDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error;
        }

        invoice.Confirm(number.Value, clock.UtcNow);
        await InvoicedQuantities.ApplyAsync(db, order.Value, invoice, cancellationToken);
        db.AddIntegrationEvent(SalesEvents.InvoiceIssued(invoice, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
