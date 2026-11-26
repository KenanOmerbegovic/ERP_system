using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Infrastructure;

/// <summary>PostgreSQL row locks for <see cref="ISalesLocks"/>; callers load entities only after locking them.</summary>
internal sealed class SalesLocks(SalesDbContext db) : ISalesLocks
{
    public Task<bool> LockOfferAsync(CompanyId companyId, Guid offerId, CancellationToken cancellationToken) =>
        LockAsync($"SELECT id AS \"Value\" FROM sales.offers WHERE company_id = {companyId.Value} AND id = {offerId} FOR UPDATE", cancellationToken);

    public Task<bool> LockOrderAsync(CompanyId companyId, Guid orderId, CancellationToken cancellationToken) =>
        LockAsync($"SELECT id AS \"Value\" FROM sales.sales_orders WHERE company_id = {companyId.Value} AND id = {orderId} FOR UPDATE", cancellationToken);

    public Task<bool> LockDeliveryNoteAsync(CompanyId companyId, Guid deliveryNoteId, CancellationToken cancellationToken) =>
        LockAsync($"SELECT id AS \"Value\" FROM sales.delivery_notes WHERE company_id = {companyId.Value} AND id = {deliveryNoteId} FOR UPDATE", cancellationToken);

    public Task<bool> LockInvoiceAsync(CompanyId companyId, Guid invoiceId, CancellationToken cancellationToken) =>
        LockAsync($"SELECT id AS \"Value\" FROM sales.sales_invoices WHERE company_id = {companyId.Value} AND id = {invoiceId} FOR UPDATE", cancellationToken);

    public Task<bool> LockCreditNoteAsync(CompanyId companyId, Guid creditNoteId, CancellationToken cancellationToken) =>
        LockAsync($"SELECT id AS \"Value\" FROM sales.credit_notes WHERE company_id = {companyId.Value} AND id = {creditNoteId} FOR UPDATE", cancellationToken);

    public async Task<int> NextNumberAsync(CompanyId companyId, SalesDocumentType documentType, int year, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var type = documentType.ToString();
        var numbers = await db.Database
            .SqlQuery<int>(
                $"""
                INSERT INTO sales.number_counters (company_id, document_type, year, last_number)
                VALUES ({companyId.Value}, {type}, {year}, 1)
                ON CONFLICT (company_id, document_type, year)
                DO UPDATE SET last_number = number_counters.last_number + 1
                RETURNING last_number AS "Value"
                """)
            .ToListAsync(cancellationToken);
        return numbers.Single();
    }

    private async Task<bool> LockAsync(FormattableString sql, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var found = await db.Database.SqlQuery<Guid>(sql).ToListAsync(cancellationToken);
        return found.Count == 1;
    }

    private void EnsureTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Row locks are held until the transaction ends; begin one first.");
        }
    }
}
