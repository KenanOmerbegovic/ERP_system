using Erp.Modules.Sales.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Invoices;

/// <summary>Deletes a draft invoice. A confirmed invoice is corrected by a storno or a credit note instead.</summary>
public sealed record DeleteSalesInvoice(CompanyId CompanyId, Guid InvoiceId);

public sealed class DeleteSalesInvoiceHandler(ISalesDbContext db, ISalesLocks locks)
{
    public async Task<Result> HandleAsync(DeleteSalesInvoice command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockInvoiceAsync(command.CompanyId, command.InvoiceId, cancellationToken))
        {
            return SalesErrors.InvoiceNotFound;
        }

        var invoice = await db.SalesInvoices.Include(i => i.Lines).Include(i => i.VatTotals).AsSplitQuery()
            .FirstAsync(i => i.Id == command.InvoiceId, cancellationToken);
        if (!invoice.IsDraft)
        {
            return SalesErrors.InvoiceNotDraft;
        }

        db.SalesInvoices.Remove(invoice);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
