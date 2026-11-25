using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Contracts;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Domain.Stock;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.StockDocuments;

/// <summary>
/// Confirms a draft (a new one, or a storno) inside the caller's transaction, after the caller locked the document:
/// locks and loads the stock it touches, posts it, numbers the document, and queues its integration event.
/// On failure nothing has been saved; the caller disposes the transaction.
/// </summary>
internal static class StockDocumentPosting
{
    public static async Task<Result> ConfirmAsync(
        IWarehouseDbContext db,
        IStockLocks locks,
        IClock clock,
        StockDocument document,
        CancellationToken cancellationToken)
    {
        var companyId = document.CompanyId;
        var series = await db.NumberSeries.AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.DocumentType == document.Type && s.ValidFrom <= document.DocumentDate)
            .OrderByDescending(s => s.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);
        if (series is null)
        {
            return WarehouseErrors.NumberSeriesNotConfigured;
        }

        var state = await LockAndLoadAsync(db, locks, document, cancellationToken);
        var posting = StockPosting.Post(document, state, clock.UtcNow);
        if (posting.Shortages.Count > 0)
        {
            return await InsufficientStockAsync(db, document, posting.Shortages, cancellationToken);
        }

        if (posting.Conflicts.Count > 0)
        {
            var first = posting.Conflicts[0];
            var items = string.Join(", ", posting.Conflicts.Select(c => c.SerialNumber ?? c.LotNumber));
            return WarehouseErrors.StockConflict(
                first.Code,
                first.Code == StockConflictCodes.SerialNumberInStock
                    ? $"Already in stock: {items}."
                    : $"The LOT already exists with another expiry date: {items}.");
        }

        db.LotBalances.AddRange(state.AddedLotBalances);
        db.StockMovements.AddRange(posting.Movements);

        var counter = await locks.NextNumberAsync(companyId, document.Type, document.DocumentDate.Year, cancellationToken);
        document.Confirm(series.Format(document.DocumentDate.Year, counter), clock.UtcNow);
        db.AddIntegrationEvent(StockEventFactory.For(document, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static async Task<StockState> LockAndLoadAsync(
        IWarehouseDbContext db, IStockLocks locks, StockDocument document, CancellationToken cancellationToken)
    {
        var companyId = document.CompanyId;
        var storehouseIds = new[] { document.SourceStorehouseId, document.TargetStorehouseId }.OfType<Guid>().ToList();
        var articleIds = document.Lines.Select(line => line.ArticleId).Distinct().ToList();

        var itemKeys = storehouseIds.SelectMany(storehouse => articleIds.Select(article => (storehouse, article))).ToList();
        await locks.LockStockItemsAsync(companyId, itemKeys, cancellationToken);

        var serialKeys = document.Lines
            .SelectMany(line => line.SerialNumbers.Select(number => (line.ArticleId, number)))
            .Distinct()
            .ToList();
        await locks.LockSerialNumbersAsync(companyId, serialKeys, cancellationToken);

        // The first receipt of a LOT sets its expiry date.
        var lotKeys = document.Lines
            .Where(line => line.LotNumber is not null)
            .GroupBy(line => (line.ArticleId, Number: line.LotNumber!))
            .Select(group => (group.Key.ArticleId, group.Key.Number, group.Select(line => line.LotExpiryDate).FirstOrDefault(date => date is not null)))
            .ToList();
        await locks.EnsureLotsAsync(companyId, lotKeys, cancellationToken);

        var items = await db.StockItems
            .Where(i => i.CompanyId == companyId && storehouseIds.Contains(i.StorehouseId) && articleIds.Contains(i.ArticleId))
            .ToListAsync(cancellationToken);
        var lotNumbers = lotKeys.Select(key => key.Number).Distinct().ToList();
        var lots = lotNumbers.Count == 0
            ? []
            : await db.Lots
                .Where(l => l.CompanyId == companyId && articleIds.Contains(l.ArticleId) && lotNumbers.Contains(l.Number))
                .ToListAsync(cancellationToken);
        var lotIds = lots.Select(lot => lot.Id).ToList();
        var lotBalances = lotIds.Count == 0
            ? []
            : await db.LotBalances
                .Where(b => b.CompanyId == companyId && storehouseIds.Contains(b.StorehouseId) && lotIds.Contains(b.LotId))
                .ToListAsync(cancellationToken);
        var serialNumbers = serialKeys.Select(key => key.number).Distinct().ToList();
        var serials = serialNumbers.Count == 0
            ? []
            : await db.SerialNumbers
                .Where(s => s.CompanyId == companyId && articleIds.Contains(s.ArticleId) && serialNumbers.Contains(s.Number))
                .ToListAsync(cancellationToken);

        return new StockState(companyId, items, lots, lotBalances, serials);
    }

    private static async Task<InsufficientStockError> InsufficientStockAsync(
        IWarehouseDbContext db, StockDocument document, IReadOnlyList<StockShortage> shortages, CancellationToken cancellationToken)
    {
        var articleIds = shortages.Select(shortage => shortage.ArticleId).Distinct().ToList();
        var codes = await db.Articles.AsNoTracking()
            .Where(a => a.CompanyId == document.CompanyId && articleIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Code, cancellationToken);
        return new InsufficientStockError(shortages
            .Select(s => new StockShortageResponse(s.StorehouseId, s.ArticleId, codes[s.ArticleId], s.LotNumber, s.SerialNumber, s.Requested, s.Available))
            .ToList());
    }
}

/// <summary>The integration event a confirmed document publishes; a storno's quantities and values are negative.</summary>
internal static class StockEventFactory
{
    public static Erp.SharedKernel.Messaging.IIntegrationEvent For(StockDocument document, DateTimeOffset now)
    {
        var sign = document.IsStorno ? -1m : 1m;
        var lines = document.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line => new StockEventLine(
                line.ArticleId,
                line.LotNumber,
                line.SerialNumbers,
                sign * line.Quantity,
                sign * (line.Value ?? 0m),
                sign * line.PriceDifference))
            .ToList();
        var companyId = document.CompanyId.Value;
        var number = document.Number ?? throw new InvalidOperationException("Only a confirmed document publishes an event.");
        return document.Type switch
        {
            StockDocumentType.Receipt => new StockReceived(
                Guid.CreateVersion7(), now, companyId, document.Id, number, document.DocumentDate,
                document.TargetStorehouseId!.Value, document.ReversesDocumentId, lines),
            StockDocumentType.Issue => new StockIssued(
                Guid.CreateVersion7(), now, companyId, document.Id, number, document.DocumentDate,
                document.SourceStorehouseId!.Value, document.ReversesDocumentId, lines),
            StockDocumentType.Transfer => new StockTransferred(
                Guid.CreateVersion7(), now, companyId, document.Id, number, document.DocumentDate,
                document.SourceStorehouseId!.Value, document.TargetStorehouseId!.Value, document.ReversesDocumentId, lines),
            _ => throw new InvalidOperationException($"Unknown document type {document.Type}."),
        };
    }
}
