using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Infrastructure;

/// <summary>PostgreSQL row locks for <see cref="IPurchasingLocks"/>; callers load entities only after locking them.</summary>
internal sealed class PurchasingLocks(PurchasingDbContext db) : IPurchasingLocks
{
    public Task<bool> LockOrderAsync(CompanyId companyId, Guid orderId, CancellationToken cancellationToken) =>
        LockAsync($"SELECT id AS \"Value\" FROM purchasing.purchase_orders WHERE company_id = {companyId.Value} AND id = {orderId} FOR UPDATE", cancellationToken);

    public Task<bool> LockReceiptAsync(CompanyId companyId, Guid receiptId, CancellationToken cancellationToken) =>
        LockAsync($"SELECT id AS \"Value\" FROM purchasing.goods_receipts WHERE company_id = {companyId.Value} AND id = {receiptId} FOR UPDATE", cancellationToken);

    public Task<bool> LockInvoiceAsync(CompanyId companyId, Guid invoiceId, CancellationToken cancellationToken) =>
        LockAsync($"SELECT id AS \"Value\" FROM purchasing.supplier_invoices WHERE company_id = {companyId.Value} AND id = {invoiceId} FOR UPDATE", cancellationToken);

    public async Task<int> NextNumberAsync(CompanyId companyId, PurchasingDocumentType documentType, int year, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var type = documentType.ToString();
        var numbers = await db.Database
            .SqlQuery<int>(
                $"""
                INSERT INTO purchasing.number_counters (company_id, document_type, year, last_number)
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
