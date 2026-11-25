using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Warehouse.Application.Stock;
using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.Modules.Warehouse.Tests.TestSupport;
using static Erp.Modules.Warehouse.Tests.TestSupport.WarehouseScenario;

namespace Erp.Modules.Warehouse.Tests.Application;

/// <summary>FR-WH-04: current stock levels per article and warehouse.</summary>
[Collection(WarehouseCollection.Name)]
public sealed class StockLevelTests(WarehouseDatabase database)
{
    private readonly WarehouseScenario _scenario = new(database);

    [Fact]
    public async Task FR_WH_04_stock_levels_per_article_warehouse_and_lot()
    {
        await _scenario.NumberSeriesAsync();
        var first = await _scenario.WarehouseAsync("MP1");
        var second = await _scenario.WarehouseAsync("MP2");
        var screws = await _scenario.ArticleAsync(code: "A001");
        var flour = await _scenario.ArticleAsync(ArticleTracking.Lot, code: "A002");
        await _scenario.ReceiveAsync(first, Line(screws, 100m, unitCost: 0.1m), Line(flour, 20m, unitCost: 1.2m, lot: "L1"));
        await _scenario.ReceiveAsync(second, Line(screws, 50m, unitCost: 0.16m));

        var all = await ListAsync(new ListStockBalances(_scenario.CompanyId, null, null, null, false, 1, 10));

        Assert.Equal(
            [("A001", "MP1", 100m, 0.1m, 10.00m), ("A001", "MP2", 50m, 0.16m, 8.00m), ("A002", "MP1", 20m, 1.2m, 24.00m)],
            all.Items.Select(b => (b.ArticleCode, b.WarehouseCode, b.Quantity, b.AverageCost, b.Value)));
        Assert.Equal(StockTrackingKind.Lot, all.Items[2].Tracking);
        Assert.Equal("L1", Assert.Single(all.Items[2].Lots).LotNumber);
        Assert.Equal(3, all.TotalCount);
    }

    [Fact]
    public async Task FR_WH_04_stock_levels_filter_and_page()
    {
        await _scenario.NumberSeriesAsync();
        var first = await _scenario.WarehouseAsync("MP1");
        var second = await _scenario.WarehouseAsync("MP2");
        var screws = await _scenario.ArticleAsync(code: "VIJAK");
        var nails = await _scenario.ArticleAsync(code: "EKSER");
        await _scenario.ReceiveAsync(first, Line(screws, 1m, unitCost: 1m), Line(nails, 1m, unitCost: 1m));
        await _scenario.ReceiveAsync(second, Line(screws, 1m, unitCost: 1m));
        await _scenario.IssueAsync(second, Line(screws, 1m));

        var inFirst = await ListAsync(new ListStockBalances(_scenario.CompanyId, first, null, null, false, 1, 10));
        var searched = await ListAsync(new ListStockBalances(_scenario.CompanyId, null, null, "vij", false, 1, 10));
        var withEmpty = await ListAsync(new ListStockBalances(_scenario.CompanyId, null, screws, null, true, 1, 10));
        var secondPage = await ListAsync(new ListStockBalances(_scenario.CompanyId, null, null, null, false, 2, 1));

        Assert.Equal(2, inFirst.TotalCount);
        Assert.Equal("VIJAK", Assert.Single(searched.Items).ArticleCode); // the empty MP2 balance is left out
        Assert.Equal(2, withEmpty.TotalCount);
        Assert.Equal("VIJAK", Assert.Single(secondPage.Items).ArticleCode);
    }

    [Fact]
    public async Task FR_WH_04_another_company_sees_none_of_the_stock()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        await _scenario.ReceiveAsync(warehouse, Line(await _scenario.ArticleAsync(), 1m, unitCost: 1m));

        var other = new WarehouseScenario(database);
        var result = await other.RunAsync(h => h.ListStockBalances.HandleAsync(
            new ListStockBalances(other.CompanyId, warehouse, null, null, true, 1, 10), CancellationToken.None));

        Assert.Empty(result.Value.Items);
    }

    private async Task<Erp.Modules.Core.Contracts.Common.PagedResponse<StockBalanceResponse>> ListAsync(ListStockBalances query)
    {
        var result = await _scenario.RunAsync(h => h.ListStockBalances.HandleAsync(query, CancellationToken.None));
        ResultAssert.Succeeds(result);
        return result.Value;
    }
}
