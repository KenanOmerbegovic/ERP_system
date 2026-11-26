using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.Modules.Sales.Domain.Invoices;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Sales.Domain.Offers;
using Erp.Modules.Sales.Domain.Orders;
using Erp.Modules.Sales.Domain.References;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Modules.Sales.Application.Abstractions;

/// <summary>The Sales module's data, implemented by Infrastructure.</summary>
public interface ISalesDbContext
{
    DbSet<CustomerReference> Customers { get; }

    DbSet<ArticleReference> Articles { get; }

    DbSet<Offer> Offers { get; }

    DbSet<SalesOrder> SalesOrders { get; }

    DbSet<DeliveryNote> DeliveryNotes { get; }

    DbSet<SalesInvoice> SalesInvoices { get; }

    DbSet<CreditNote> CreditNotes { get; }

    DbSet<NumberSeries> NumberSeries { get; }

    /// <summary>Queues the event in Sales' outbox; it is saved by the next <see cref="SaveChangesAsync"/>.</summary>
    void AddIntegrationEvent(IIntegrationEvent integrationEvent);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <summary>Forgets every tracked entity, so the next query reads current values (after a lock).</summary>
    void ClearTracking();
}

/// <summary>
/// Row locks (<c>SELECT … FOR UPDATE</c>) for confirmations. A document is locked first, then the document it counts
/// against: delivery notes and invoices lock their sales order, credit notes their invoice (and then its order), so
/// delivered, invoiced and credited quantities are serialized. Call inside a transaction; locks are held until it ends.
/// </summary>
public interface ISalesLocks
{
    Task<bool> LockOfferAsync(CompanyId companyId, Guid offerId, CancellationToken cancellationToken);

    Task<bool> LockOrderAsync(CompanyId companyId, Guid orderId, CancellationToken cancellationToken);

    Task<bool> LockDeliveryNoteAsync(CompanyId companyId, Guid deliveryNoteId, CancellationToken cancellationToken);

    Task<bool> LockInvoiceAsync(CompanyId companyId, Guid invoiceId, CancellationToken cancellationToken);

    Task<bool> LockCreditNoteAsync(CompanyId companyId, Guid creditNoteId, CancellationToken cancellationToken);

    /// <summary>The next counter for the document type and year; gapless, as it rolls back with the transaction.</summary>
    Task<int> NextNumberAsync(CompanyId companyId, SalesDocumentType documentType, int year, CancellationToken cancellationToken);
}
