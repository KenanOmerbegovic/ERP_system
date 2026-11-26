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

/// <summary>Reverses a registered supplier invoice with a storno dated today; its quantities can be invoiced again.</summary>
public sealed record StornoSupplierInvoice(CompanyId CompanyId, Guid InvoiceId);

public sealed class StornoSupplierInvoiceHandler(IPurchasingDbContext db, IPurchasingLocks locks, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(StornoSupplierInvoice command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockInvoiceAsync(command.CompanyId, command.InvoiceId, cancellationToken))
        {
            return PurchasingErrors.InvoiceNotFound;
        }

        var orderId = await db.SupplierInvoices.AsNoTracking().Where(i => i.Id == command.InvoiceId).Select(i => i.PurchaseOrderId).FirstAsync(cancellationToken);
        await locks.LockOrderAsync(command.CompanyId, orderId, cancellationToken);
        var invoice = await db.SupplierInvoices.Include(i => i.Lines).Include(i => i.VatTotals).FirstAsync(i => i.Id == command.InvoiceId, cancellationToken);
        if (invoice.IsDraft)
        {
            return PurchasingErrors.InvoiceNotConfirmed;
        }

        if (invoice.IsStorno)
        {
            return PurchasingErrors.IsStorno;
        }

        if (invoice.ReversedByInvoiceId is not null)
        {
            return PurchasingErrors.AlreadyReversed;
        }

        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.Id == orderId, cancellationToken);
        var storno = invoice.CreateStorno(clock.Today, clock.UtcNow);
        var number = await DocumentNumbers.NextAsync(db, locks, command.CompanyId, PurchasingDocumentType.SupplierInvoice, storno.InvoiceDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error;
        }

        storno.Confirm(number.Value, clock.UtcNow);
        db.SupplierInvoices.Add(storno);
        await InvoicedQuantities.ApplyAsync(db, order, storno, cancellationToken);
        db.AddIntegrationEvent(PurchasingEvents.InvoiceRegistered(storno, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return storno.Id;
    }
}
