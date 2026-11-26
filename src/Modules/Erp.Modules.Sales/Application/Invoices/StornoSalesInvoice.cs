using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Invoices;

/// <summary>
/// Reverses a confirmed invoice in full with a storno dated today (FR-SAL-05); its quantities can be invoiced again. Only
/// while no credit note was made on it: after that, the rest is corrected by further credit notes.
/// </summary>
public sealed record StornoSalesInvoice(CompanyId CompanyId, Guid InvoiceId);

public sealed class StornoSalesInvoiceHandler(ISalesDbContext db, ISalesLocks locks, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(StornoSalesInvoice command, CancellationToken cancellationToken)
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
        if (invoice.IsDraft)
        {
            return SalesErrors.InvoiceNotConfirmed;
        }

        if (invoice.IsStorno)
        {
            return SalesErrors.IsStorno;
        }

        if (invoice.ReversedByInvoiceId is not null)
        {
            return SalesErrors.AlreadyReversed;
        }

        // Credit notes being confirmed count as made: their returns may already be in the warehouse.
        if (!invoice.CanBeReversed
            || await db.CreditNotes.AnyAsync(c => c.InvoiceId == invoice.Id && c.Status == DocumentStatus.Confirming, cancellationToken))
        {
            return SalesErrors.InvoiceCredited;
        }

        var order = await db.SalesOrders.Include(o => o.Lines).FirstAsync(o => o.Id == orderId, cancellationToken);
        var storno = invoice.CreateStorno(clock.Today, clock.UtcNow);
        var number = await DocumentNumbers.NextAsync(db, locks, command.CompanyId, SalesDocumentType.SalesInvoice, storno.InvoiceDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error;
        }

        storno.Confirm(number.Value, clock.UtcNow);
        db.SalesInvoices.Add(storno);
        await InvoicedQuantities.ApplyAsync(db, order, storno, cancellationToken);
        db.AddIntegrationEvent(SalesEvents.InvoiceIssued(storno, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return storno.Id;
    }
}
