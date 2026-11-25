using Erp.Modules.Warehouse.Application;
using Erp.Modules.Warehouse.Application.StockDocuments;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Tests.TestSupport;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using static Erp.Modules.Warehouse.Tests.TestSupport.WarehouseScenario;

namespace Erp.Modules.Warehouse.Tests.Application;

/// <summary>FR-WH-03: the system shall prevent issuing more stock than is available, also under concurrent requests.</summary>
[Collection(WarehouseCollection.Name)]
public sealed class AvailabilityTests(WarehouseDatabase database)
{
    private readonly WarehouseScenario _scenario = new(database);

    [Fact]
    public async Task FR_WH_03_issue_beyond_available_stock_is_rejected()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync(code: "BRASNO");
        var other = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 1m), Line(other, 10m, unitCost: 1m));
        var draft = await _scenario.DraftAsync(StockDocumentType.Issue, warehouse, null, Line(other, 3m), Line(article, 10.5m));

        var result = await _scenario.ConfirmAsync(draft.Value);

        var error = Assert.IsType<InsufficientStockError>(result.Error);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(new StockShortageResponse(warehouse, article, "BRASNO", null, null, 10.5m, 10m), Assert.Single(error.Shortages));
        // Nothing was saved: not the other line, not the document's confirmation.
        Assert.Equal(10m, (await _scenario.StockAsync(warehouse, other))!.Quantity);
        Assert.True((await _scenario.DocumentAsync(draft.Value)).IsDraft);
        Assert.False(await _scenario.QueryAsync(db => db.StockMovements.AnyAsync(m => m.DocumentId == draft.Value)));
    }

    [Fact]
    public async Task FR_WH_03_issue_from_empty_warehouse_is_rejected()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var draft = await _scenario.DraftAsync(StockDocumentType.Transfer, warehouse, await _scenario.WarehouseAsync("MP2"), Line(article, 1m));

        ResultAssert.FailsWithCode(await _scenario.ConfirmAsync(draft.Value), InsufficientStockError.ErrorCode);
    }

    [Fact]
    public async Task FR_WH_03_lines_of_one_document_draw_on_the_same_stock()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 1m));
        var draft = await _scenario.DraftAsync(StockDocumentType.Issue, warehouse, null, Line(article, 6m), Line(article, 6m));

        var result = await _scenario.ConfirmAsync(draft.Value);

        var shortage = Assert.Single(Assert.IsType<InsufficientStockError>(result.Error).Shortages);
        Assert.Equal((6m, 4m), (shortage.Requested, shortage.Available));
    }

    [Fact]
    public async Task FR_WH_03_storno_of_consumed_receipt_is_rejected()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var receipt = await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 1m));
        await _scenario.IssueAsync(warehouse, Line(article, 8m));

        var result = await _scenario.StornoAsync(receipt);

        ResultAssert.FailsWithCode(result, InsufficientStockError.ErrorCode);
        Assert.Null((await _scenario.DocumentAsync(receipt)).ReversedByDocumentId);
        Assert.Equal(2m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
    }

    [Fact]
    public async Task FR_WH_03_concurrent_issues_never_overdraw_stock()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 1m));
        var quantities = Enumerable.Repeat(1m, 20).Concat([7m, 7m]).ToList(); // asks for 34 of 10
        var drafts = new List<(Guid Id, decimal Quantity)>();
        foreach (var quantity in quantities)
        {
            drafts.Add(((await _scenario.DraftAsync(StockDocumentType.Issue, warehouse, null, Line(article, quantity))).Value, quantity));
        }

        // Each confirmation on its own context and connection, released together.
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmations = drafts.Select(draft => Task.Run(async () =>
        {
            await start.Task;
            return (draft.Quantity, Result: await _scenario.ConfirmAsync(draft.Id));
        })).ToList();
        start.SetResult();
        var results = await Task.WhenAll(confirmations);

        Assert.All(results, r => Assert.True(r.Result.IsSuccess || r.Result.Error is InsufficientStockError, r.Result.Error.Code));
        var issued = results.Where(r => r.Result.IsSuccess).Sum(r => r.Quantity);
        var stock = await _scenario.StockAsync(warehouse, article);
        Assert.True(issued <= 10m, $"Issued {issued} of 10.");
        Assert.Equal(10m - issued, stock!.Quantity);
        Assert.True(stock.Quantity >= 0m);
        Assert.Equal(stock.Quantity, await _scenario.QueryAsync(db => db.StockMovements
            .Where(m => m.StorehouseId == warehouse && m.ArticleId == article).SumAsync(m => m.Quantity)));
        Assert.Equal(stock.Value, await _scenario.QueryAsync(db => db.StockMovements
            .Where(m => m.StorehouseId == warehouse && m.ArticleId == article).SumAsync(m => m.Value)));
        // Every success got its own number, with no gaps.
        var numbers = await _scenario.QueryAsync(db => db.StockDocuments
            .Where(d => d.CompanyId == _scenario.CompanyId && d.Type == StockDocumentType.Issue && d.Number != null)
            .Select(d => d.Number!).ToListAsync());
        Assert.Equal(
            Enumerable.Range(1, results.Count(r => r.Result.IsSuccess)).Select(n => $"IZD-{_scenario.Today.Year}-{n:D5}").Order(),
            numbers.Order());
    }

    [Fact]
    public async Task FR_WH_03_concurrent_confirmations_of_one_document_move_stock_once()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var draft = (await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 5m, unitCost: 1m))).Value;

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => _scenario.ConfirmAsync(draft))));

        Assert.Single(results, r => r.IsSuccess);
        Assert.All(results.Where(r => r.IsFailure), r => Assert.Equal(WarehouseErrors.StockDocumentNotDraft, r.Error));
        Assert.Equal(5m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
    }

    [Fact]
    public async Task FR_WH_03_concurrent_transfers_in_opposite_directions_do_not_deadlock()
    {
        await _scenario.NumberSeriesAsync();
        var first = await _scenario.WarehouseAsync("MP1");
        var second = await _scenario.WarehouseAsync("MP2");
        var articleA = await _scenario.ArticleAsync();
        var articleB = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(first, Line(articleA, 100m, unitCost: 1m), Line(articleB, 100m, unitCost: 1m));
        await _scenario.ReceiveAsync(second, Line(articleA, 100m, unitCost: 1m), Line(articleB, 100m, unitCost: 1m));
        var drafts = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            var (from, to) = i % 2 == 0 ? (first, second) : (second, first);
            var lines = i % 2 == 0 ? new[] { Line(articleA, 1m), Line(articleB, 1m) } : [Line(articleB, 1m), Line(articleA, 1m)];
            drafts.Add((await _scenario.DraftAsync(StockDocumentType.Transfer, from, to, lines)).Value);
        }

        var results = await Task.WhenAll(drafts.Select(id => Task.Run(() => _scenario.ConfirmAsync(id))));

        Assert.All(results, ResultAssert.Succeeds);
        Assert.Equal(100m, (await _scenario.StockAsync(first, articleA))!.Quantity);
    }
}
