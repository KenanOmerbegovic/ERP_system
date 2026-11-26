using Erp.Modules.Purchasing.Domain.Invoices;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.Modules.Purchasing.Domain.References;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Modules.Purchasing.Application.Abstractions;

/// <summary>The Purchasing module's data, implemented by Infrastructure.</summary>
public interface IPurchasingDbContext
{
    DbSet<SupplierReference> Suppliers { get; }

    DbSet<ArticleReference> Articles { get; }

    DbSet<PurchaseOrder> PurchaseOrders { get; }

    DbSet<GoodsReceipt> GoodsReceipts { get; }

    DbSet<SupplierInvoice> SupplierInvoices { get; }

    DbSet<NumberSeries> NumberSeries { get; }

    /// <summary>Queues the event in Purchasing's outbox; it is saved by the next <see cref="SaveChangesAsync"/>.</summary>
    void AddIntegrationEvent(IIntegrationEvent integrationEvent);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <summary>Forgets every tracked entity, so the next query reads current values (after a lock).</summary>
    void ClearTracking();
}

/// <summary>
/// Row locks (<c>SELECT … FOR UPDATE</c>) for confirmations. The purchase order is the serialization point for its
/// received and invoiced quantities: every receipt and invoice of an order locks the order after locking itself.
/// Call inside a transaction; locks are held until it ends.
/// </summary>
public interface IPurchasingLocks
{
    Task<bool> LockOrderAsync(CompanyId companyId, Guid orderId, CancellationToken cancellationToken);

    Task<bool> LockReceiptAsync(CompanyId companyId, Guid receiptId, CancellationToken cancellationToken);

    Task<bool> LockInvoiceAsync(CompanyId companyId, Guid invoiceId, CancellationToken cancellationToken);

    /// <summary>The next counter for the document type and year; gapless, as it rolls back with the transaction.</summary>
    Task<int> NextNumberAsync(CompanyId companyId, PurchasingDocumentType documentType, int year, CancellationToken cancellationToken);
}
