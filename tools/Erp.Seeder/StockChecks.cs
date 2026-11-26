using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Stock;
using Erp.Modules.Warehouse.Application.StockDocuments;
using Erp.Modules.Warehouse.Application.Storehouses;
using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.Modules.Warehouse.Domain.Articles;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Seeder.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Seeder;

/// <summary>
/// FR-GEN-03, consistent stock levels: the stock read back through Warehouse's queries equals the generated opening
/// receipts plus the purchased goods, and the stock ledger agrees with itself (balances = sum of movements, nothing negative, every serial
/// number in stock counted once).
/// </summary>
internal static class StockChecks
{
    private const int ListStockBalancesPageSize = 100;

    public static async Task RunAsync(IServiceProvider provider, SeedData data, SeededCompany seeded, List<string> problems, CancellationToken cancellationToken)
    {
        var warehouses = await provider.GetRequiredService<ListWarehousesHandler>()
            .HandleAsync(new ListWarehouses(seeded.CompanyId), cancellationToken);
        Expect(problems, "warehouses", data.Warehouses.Count, warehouses.Count);

        var documents = await provider.GetRequiredService<ListStockDocumentsHandler>().HandleAsync(
            new ListStockDocuments(seeded.CompanyId, StockDocumentType.Receipt, StockDocumentStatus.Confirmed, null, null, null, null, 1, 1),
            cancellationToken);
        var purchased = seeded.PurchaseOrders.Count(order => order.ReceiptId is not null);
        Expect(problems, "confirmed stock receipts (opening and purchased)", data.OpeningReceipts.Count + purchased, documents.IsSuccess ? documents.Value.TotalCount : -1);

        await CheckBalancesAgainstReceiptsAsync(provider, data, seeded, problems, cancellationToken);
        await CheckLedgerAsync(provider.GetRequiredService<IWarehouseDbContext>(), seeded, problems, cancellationToken);
    }

    private static async Task CheckBalancesAgainstReceiptsAsync(
        IServiceProvider provider, SeedData data, SeededCompany seeded, List<string> problems, CancellationToken cancellationToken)
    {
        // Opening stock, plus what the purchasing scenario received into the first warehouse at the order price.
        var firstWarehouse = seeded.WarehouseIds[data.Warehouses[0].Code];
        var opening = data.OpeningReceipts
            .SelectMany(receipt => receipt.Lines.Select(line => (
                Warehouse: seeded.WarehouseIds[receipt.WarehouseCode], Article: seeded.ArticleIds[line.ArticleCode], line.Quantity, line.Value)));
        var purchased = seeded.PurchaseOrders
            .Where(order => order.ReceiptId is not null)
            .SelectMany(order => order.Seed.Lines
                .Where(line => line.ReceivedQuantity(order.Seed.ReceivedShare) > 0m)
                .Select(line => (
                    Warehouse: firstWarehouse,
                    Article: seeded.ArticleIds[line.ArticleCode],
                    Quantity: line.ReceivedQuantity(order.Seed.ReceivedShare),
                    Value: line.ReceivedValue(order.Seed.ReceivedShare))));
        var expected = opening.Concat(purchased)
            .GroupBy(entry => (entry.Warehouse, entry.Article))
            .ToDictionary(group => group.Key, group => (Quantity: group.Sum(e => e.Quantity), Value: group.Sum(e => e.Value)));

        var handler = provider.GetRequiredService<ListStockBalancesHandler>();
        var actual = new List<StockBalanceResponse>();
        for (var page = 1; ; page++)
        {
            var result = await handler.HandleAsync(
                new ListStockBalances(seeded.CompanyId, null, null, null, IncludeEmpty: false, page, ListStockBalancesPageSize), cancellationToken);
            if (!result.IsSuccess)
            {
                problems.Add($"Stock levels could not be read: {MockDataSeeder.Describe(result.Error)}");
                return;
            }

            actual.AddRange(result.Value.Items);
            if (actual.Count >= result.Value.TotalCount || result.Value.Items.Count == 0)
            {
                break;
            }
        }

        Expect(problems, "stock balances", expected.Count, actual.Count);
        foreach (var balance in actual)
        {
            if (!expected.TryGetValue((balance.WarehouseId, balance.ArticleId), out var want))
            {
                problems.Add($"Unexpected stock of {balance.ArticleCode} in {balance.WarehouseCode}.");
            }
            else if (balance.Quantity != want.Quantity || balance.Value != want.Value)
            {
                problems.Add($"{balance.ArticleCode} in {balance.WarehouseCode}: expected {want.Quantity} worth {want.Value} KM, found {balance.Quantity} worth {balance.Value} KM.");
            }
        }
    }

    private static async Task CheckLedgerAsync(IWarehouseDbContext db, SeededCompany seeded, List<string> problems, CancellationToken cancellationToken)
    {
        var companyId = seeded.CompanyId;
        var items = await db.StockItems.AsNoTracking().Where(i => i.CompanyId == companyId).ToListAsync(cancellationToken);
        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.CompanyId == companyId)
            .GroupBy(m => new { m.StorehouseId, m.ArticleId })
            .Select(g => new { g.Key.StorehouseId, g.Key.ArticleId, Quantity = g.Sum(m => m.Quantity), Value = g.Sum(m => m.Value) })
            .ToDictionaryAsync(m => (m.StorehouseId, m.ArticleId), cancellationToken);
        foreach (var item in items)
        {
            var ledger = movements.GetValueOrDefault((item.StorehouseId, item.ArticleId));
            if (item.Quantity < 0m || item.Value < 0m || (item.Quantity == 0m && item.Value != 0m))
            {
                problems.Add($"Stock item {item.Id} has quantity {item.Quantity} and value {item.Value}.");
            }

            if (item.Quantity != (ledger?.Quantity ?? 0m) || item.Value != (ledger?.Value ?? 0m))
            {
                problems.Add($"Stock item {item.Id} ({item.Quantity}, {item.Value} KM) differs from its movements ({ledger?.Quantity}, {ledger?.Value} KM).");
            }
        }

        var lotBalances = await db.LotBalances.AsNoTracking().Where(b => b.CompanyId == companyId).ToListAsync(cancellationToken);
        var lotMovements = await db.StockMovements.AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.LotId != null)
            .GroupBy(m => new { m.StorehouseId, LotId = m.LotId!.Value })
            .Select(g => new { g.Key.StorehouseId, g.Key.LotId, Quantity = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(m => (m.StorehouseId, m.LotId), m => m.Quantity, cancellationToken);
        foreach (var balance in lotBalances.Where(b => b.Quantity != lotMovements.GetValueOrDefault((b.StorehouseId, b.LotId))))
        {
            problems.Add($"LOT balance {balance.Id} ({balance.Quantity}) differs from its movements.");
        }

        var lotTotals = lotBalances.GroupBy(b => (b.StorehouseId, b.ArticleId)).ToDictionary(g => g.Key, g => g.Sum(b => b.Quantity));
        var serialsInStock = await db.SerialNumbers.AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.StorehouseId != null)
            .GroupBy(s => new { StorehouseId = s.StorehouseId!.Value, s.ArticleId })
            .Select(g => new { g.Key.StorehouseId, g.Key.ArticleId, Count = g.Count() })
            .ToDictionaryAsync(s => (s.StorehouseId, s.ArticleId), s => s.Count, cancellationToken);
        var tracking = await db.Articles.AsNoTracking().Where(a => a.CompanyId == companyId).ToDictionaryAsync(a => a.Id, a => a.Tracking, cancellationToken);
        foreach (var item in items)
        {
            var key = (item.StorehouseId, item.ArticleId);
            if (tracking[item.ArticleId] == TrackingType.Lot && lotTotals.GetValueOrDefault(key) != item.Quantity)
            {
                problems.Add($"Stock item {item.Id}: its LOTs hold {lotTotals.GetValueOrDefault(key)}, the item {item.Quantity}.");
            }

            if (tracking[item.ArticleId] == TrackingType.Serial && serialsInStock.GetValueOrDefault(key) != item.Quantity)
            {
                problems.Add($"Stock item {item.Id}: {serialsInStock.GetValueOrDefault(key)} serial numbers in stock, the item holds {item.Quantity}.");
            }
        }
    }

    private static void Expect(List<string> problems, string what, int expected, int actual)
    {
        if (expected != actual)
        {
            problems.Add($"Expected {expected} {what}, found {actual}.");
        }
    }
}
