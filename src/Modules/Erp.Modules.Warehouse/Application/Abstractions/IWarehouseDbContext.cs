using Erp.Modules.Warehouse.Domain.Articles;
using Erp.Modules.Warehouse.Domain.Numbering;
using Erp.Modules.Warehouse.Domain.Stock;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Domain.Storehouses;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Modules.Warehouse.Application.Abstractions;

/// <summary>The Warehouse module's data, implemented by Infrastructure.</summary>
public interface IWarehouseDbContext
{
    DbSet<ArticleReference> Articles { get; }

    DbSet<Storehouse> Storehouses { get; }

    DbSet<StockDocument> StockDocuments { get; }

    DbSet<StockItem> StockItems { get; }

    DbSet<Lot> Lots { get; }

    DbSet<LotBalance> LotBalances { get; }

    DbSet<SerialNumber> SerialNumbers { get; }

    DbSet<StockMovement> StockMovements { get; }

    DbSet<NumberSeries> NumberSeries { get; }

    /// <summary>Queues the event in Warehouse's outbox; it is saved by the next <see cref="SaveChangesAsync"/>.</summary>
    void AddIntegrationEvent(IIntegrationEvent integrationEvent);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Row locks (PostgreSQL <c>SELECT … FOR UPDATE</c>) that serialize everything which moves stock, so concurrent
/// documents can never take the same goods twice (FR-WH-03). Call inside a transaction; locks are held until it ends.
/// Callers lock in one order to avoid deadlocks: document, stock items, serial numbers, then the number counter.
/// Rows a lock needs are created first if missing, so a lock always has a row to hold.
/// </summary>
public interface IStockLocks
{
    /// <summary>Locks the document; false if the company has no such document.</summary>
    Task<bool> LockDocumentAsync(CompanyId companyId, Guid documentId, CancellationToken cancellationToken);

    /// <summary>Creates missing stock items (empty) and locks them, sorted by warehouse and article.</summary>
    Task LockStockItemsAsync(CompanyId companyId, IReadOnlyCollection<(Guid StorehouseId, Guid ArticleId)> keys, CancellationToken cancellationToken);

    /// <summary>Creates missing serial numbers (not in stock) and locks them, sorted by article and number.</summary>
    Task LockSerialNumbersAsync(CompanyId companyId, IReadOnlyCollection<(Guid ArticleId, string Number)> keys, CancellationToken cancellationToken);

    /// <summary>Creates missing LOTs with the given expiry date. Existing LOTs keep theirs.</summary>
    Task EnsureLotsAsync(CompanyId companyId, IReadOnlyCollection<(Guid ArticleId, string Number, DateOnly? ExpiryDate)> lots, CancellationToken cancellationToken);

    /// <summary>The next counter for the document type and year; the counter row stays locked until the transaction ends.</summary>
    Task<int> NextNumberAsync(CompanyId companyId, StockDocumentType documentType, int year, CancellationToken cancellationToken);
}
