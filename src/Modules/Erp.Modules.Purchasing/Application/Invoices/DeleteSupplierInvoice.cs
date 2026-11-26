using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Invoices;

/// <summary>Deletes a draft invoice. A registered one is reversed with a storno.</summary>
public sealed record DeleteSupplierInvoice(CompanyId CompanyId, Guid InvoiceId);

public sealed class DeleteSupplierInvoiceHandler(IPurchasingDbContext db, IPurchasingLocks locks)
{
    public async Task<Result> HandleAsync(DeleteSupplierInvoice command, CancellationToken cancellationToken)
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

        db.SupplierInvoices.Remove(invoice);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
