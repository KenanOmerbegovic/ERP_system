using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Warehouse.Application;
using Erp.Modules.Warehouse.Application.Stock;
using Erp.Modules.Warehouse.Domain.Stock;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using static Erp.Modules.Warehouse.Tests.TestSupport.WarehouseScenario;

namespace Erp.Modules.Warehouse.Tests.Application;

/// <summary>FR-WH-02: LOT and serial numbers for articles that require them.</summary>
[Collection(WarehouseCollection.Name)]
public sealed class TrackingTests(WarehouseDatabase database)
{
    private readonly WarehouseScenario _scenario = new(database);

    [Fact]
    public async Task FR_WH_02_lot_tracked_line_requires_lot()
    {
        var warehouse = await _scenario.WarehouseAsync();
        var tracked = await _scenario.ArticleAsync(ArticleTracking.Lot);
        var untracked = await _scenario.ArticleAsync();

        var missing = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(tracked, 1m, unitCost: 1m));
        var notAllowed = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(untracked, 1m, unitCost: 1m, lot: "L1"));

        ResultAssert.FailsValidation(missing, "Lines[0].LotNumber", WarehouseValidationCodes.LotNumberRequired);
        ResultAssert.FailsValidation(notAllowed, "Lines[0].LotNumber", WarehouseValidationCodes.LotNumberNotAllowed);
    }

    [Fact]
    public async Task FR_WH_02_stock_is_kept_per_lot_with_one_average_per_article_and_warehouse()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync(ArticleTracking.Lot);
        var expiry = new DateOnly(2027, 6, 30);
        await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 2m, lot: "L-01", expiry: expiry), Line(article, 10m, unitCost: 4m, lot: "L-02"));

        await _scenario.IssueAsync(warehouse, Line(article, 4m, lot: "L-01"));

        var balances = await _scenario.RunAsync(h => h.ListStockBalances.HandleAsync(
            new ListStockBalances(_scenario.CompanyId, warehouse, article, null, false, 1, 10), CancellationToken.None));
        var balance = Assert.Single(balances.Value.Items);
        Assert.Equal((16m, 3.00m, 48.00m), (balance.Quantity, balance.AverageCost, balance.Value)); // issued at the shared average
        Assert.Equal(
            [("L-01", (DateOnly?)expiry, 6m), ("L-02", null, 10m)],
            balance.Lots.Select(lot => (lot.LotNumber, lot.ExpiryDate, lot.Quantity)).OrderBy(lot => lot.LotNumber));
    }

    [Fact]
    public async Task FR_WH_02_issue_beyond_lot_quantity_is_rejected()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync(ArticleTracking.Lot);
        await _scenario.ReceiveAsync(warehouse, Line(article, 5m, unitCost: 1m, lot: "A"), Line(article, 5m, unitCost: 1m, lot: "B"));
        var draft = await _scenario.DraftAsync(StockDocumentType.Issue, warehouse, null, Line(article, 6m, lot: "A"));

        var result = await _scenario.ConfirmAsync(draft.Value);

        var error = Assert.IsType<InsufficientStockError>(result.Error);
        var shortage = Assert.Single(error.Shortages);
        Assert.Equal(("A", 6m, 5m), (shortage.LotNumber, shortage.Requested, shortage.Available));
    }

    [Fact]
    public async Task FR_WH_02_lot_keeps_the_expiry_date_of_its_first_receipt()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync(ArticleTracking.Lot);
        await _scenario.ReceiveAsync(warehouse, Line(article, 1m, unitCost: 1m, lot: "A", expiry: new DateOnly(2027, 1, 31)));
        var draft = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 1m, unitCost: 1m, lot: "A", expiry: new DateOnly(2027, 2, 28)));

        ResultAssert.FailsWithCode(await _scenario.ConfirmAsync(draft.Value), StockConflictCodes.LotExpiryMismatch);
    }

    [Fact]
    public async Task FR_WH_02_serial_count_must_equal_quantity()
    {
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync(ArticleTracking.Serial);
        var untracked = await _scenario.ArticleAsync();

        var tooFew = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 2m, 100m, null, null, "SN-1"));
        var duplicate = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse,
            Line(article, 1m, 100m, null, null, "SN-1"), Line(article, 1m, 100m, null, null, "SN-1"));
        var notAllowed = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(untracked, 1m, 1m, null, null, "X"));

        ResultAssert.FailsValidation(tooFew, "Lines[0].SerialNumbers", WarehouseValidationCodes.SerialNumberCountMismatch);
        ResultAssert.FailsValidation(duplicate, "Lines[1].SerialNumbers", WarehouseValidationCodes.SerialNumberDuplicate);
        ResultAssert.FailsValidation(notAllowed, "Lines[0].SerialNumbers", WarehouseValidationCodes.SerialNumbersNotAllowed);
    }

    [Fact]
    public async Task FR_WH_02_serial_cannot_be_received_twice()
    {
        await _scenario.NumberSeriesAsync();
        var first = await _scenario.WarehouseAsync("MP1");
        var second = await _scenario.WarehouseAsync("MP2");
        var article = await _scenario.ArticleAsync(ArticleTracking.Serial);
        await _scenario.ReceiveAsync(first, Line(article, 2m, 500m, null, null, "SN-1", "SN-2"));
        var draft = await _scenario.DraftAsync(StockDocumentType.Receipt, null, second, Line(article, 1m, 500m, null, null, "SN-2"));

        ResultAssert.FailsWithCode(await _scenario.ConfirmAsync(draft.Value), StockConflictCodes.SerialNumberInStock);
    }

    [Fact]
    public async Task FR_WH_02_issue_of_serial_not_in_warehouse_is_rejected()
    {
        await _scenario.NumberSeriesAsync();
        var first = await _scenario.WarehouseAsync("MP1");
        var second = await _scenario.WarehouseAsync("MP2");
        var article = await _scenario.ArticleAsync(ArticleTracking.Serial);
        await _scenario.ReceiveAsync(first, Line(article, 1m, 500m, null, null, "SN-1"));
        await _scenario.ReceiveAsync(second, Line(article, 1m, 500m, null, null, "SN-2"));
        var draft = await _scenario.DraftAsync(StockDocumentType.Issue, second, null, Line(article, 1m, null, null, null, "SN-1"));

        var result = await _scenario.ConfirmAsync(draft.Value);

        var shortage = Assert.Single(Assert.IsType<InsufficientStockError>(result.Error).Shortages);
        Assert.Equal(("SN-1", second), (shortage.SerialNumber, shortage.WarehouseId));
    }

    [Fact]
    public async Task FR_WH_02_serial_numbers_follow_transfers_and_issues()
    {
        await _scenario.NumberSeriesAsync();
        var first = await _scenario.WarehouseAsync("MP1");
        var second = await _scenario.WarehouseAsync("MP2");
        var article = await _scenario.ArticleAsync(ArticleTracking.Serial);
        await _scenario.ReceiveAsync(first, Line(article, 3m, 500m, null, null, "SN-1", "SN-2", "SN-3"));

        await _scenario.PostAsync(StockDocumentType.Transfer, first, second, Line(article, 1m, null, null, null, "SN-2"));
        var issue = await _scenario.IssueAsync(first, Line(article, 1m, null, null, null, "SN-3"));

        var serials = await _scenario.RunAsync(h => h.ListSerialNumbers.HandleAsync(
            new ListSerialNumbers(_scenario.CompanyId, article, null, false, null, 1, 10), CancellationToken.None));
        Assert.Equal(
            [("SN-1", true, (Guid?)first), ("SN-2", true, second), ("SN-3", false, null)],
            serials.Value.Items.Select(s => (s.SerialNumber, s.InStock, s.WarehouseId)));

        ResultAssert.Succeeds(await _scenario.StornoAsync(issue));
        var back = await _scenario.QueryAsync(db => db.SerialNumbers.SingleAsync(s => s.ArticleId == article && s.Number == "SN-3"));
        Assert.Equal(first, back.StorehouseId);
    }
}
