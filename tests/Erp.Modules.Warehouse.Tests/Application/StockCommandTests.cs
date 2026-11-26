using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Application;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Tests.TestSupport;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using static Erp.Modules.Warehouse.Tests.TestSupport.WarehouseScenario;

namespace Erp.Modules.Warehouse.Tests.Application;

/// <summary>The synchronous, idempotent command contract other modules move stock through (FR-PUR-01 receipts).</summary>
[Collection(WarehouseCollection.Name)]
public sealed class StockCommandTests(WarehouseDatabase database)
{
    private readonly WarehouseScenario _scenario = new(database);

    [Fact]
    public async Task FR_PUR_01_same_receipt_sent_twice_moves_stock_once()
    {
        var (warehouse, article) = await SetUpAsync();
        var command = Receipt(warehouse, article, 5m, 2.5m);

        var first = await ReceiveAsync(command);
        var second = await ReceiveAsync(command);

        ResultAssert.Succeeds(first);
        Assert.Equal(first.Value, second.Value with { LineValues = first.Value.LineValues });
        Assert.Equal([12.50m], second.Value.LineValues);
        Assert.Equal(5m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
        var document = await _scenario.DocumentAsync(first.Value.DocumentId);
        Assert.Equal(("purchasing", command.Key.SourceDocumentId), (document.SourceModule, document.SourceDocumentId));
    }

    [Fact]
    public async Task FR_PUR_01_same_receipt_sent_concurrently_moves_stock_once()
    {
        var (warehouse, article) = await SetUpAsync();
        var command = Receipt(warehouse, article, 3m, 1m);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => ReceiveAsync(command))));

        Assert.All(results, ResultAssert.Succeeds);
        Assert.Single(results.Select(r => r.Value.DocumentId).Distinct());
        Assert.Equal(3m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
        Assert.Equal(1, await _scenario.QueryAsync(db => db.StockDocuments.CountAsync(d => d.CompanyId == _scenario.CompanyId)));
    }

    [Fact]
    public async Task Reused_key_with_different_payload_is_rejected()
    {
        var (warehouse, article) = await SetUpAsync();
        var command = Receipt(warehouse, article, 3m, 1m);
        ResultAssert.Succeeds(await ReceiveAsync(command));

        var changed = await ReceiveAsync(command with { Lines = [command.Lines[0] with { Quantity = 4m }] });

        ResultAssert.Fails(changed, StockCommandErrors.IdempotencyKeyReused);
        Assert.Equal(3m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
    }

    [Fact]
    public async Task Failed_command_leaves_nothing_behind_and_can_be_retried()
    {
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var command = Receipt(warehouse, article, 2m, 1m);

        var withoutSeries = await ReceiveAsync(command);
        var invalid = await ReceiveAsync(command with { Lines = [command.Lines[0] with { ArticleId = Guid.NewGuid() }] });
        await _scenario.NumberSeriesAsync();
        var retried = await ReceiveAsync(command);

        ResultAssert.Fails(withoutSeries, WarehouseErrors.NumberSeriesNotConfigured);
        ResultAssert.FailsValidation(invalid, "Lines[0].ArticleId", WarehouseValidationCodes.ArticleNotFound);
        ResultAssert.Succeeds(retried);
        Assert.Equal(2m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
    }

    [Fact]
    public async Task Owned_stock_document_is_reversed_only_through_the_contract_and_once()
    {
        var (warehouse, article) = await SetUpAsync();
        var receipt = Receipt(warehouse, article, 4m, 2m);
        var received = await ReceiveAsync(receipt);

        ResultAssert.Fails(await _scenario.StornoAsync(received.Value.DocumentId), WarehouseErrors.StockDocumentOwnedByAnotherModule);

        var reversal = new StockReversalCommand(new IdempotencyKey("purchasing-storno", receipt.Key.SourceDocumentId), _scenario.CompanyId, receipt.Key);
        var first = await ReverseAsync(reversal);
        var again = await ReverseAsync(reversal);

        ResultAssert.Succeeds(first);
        Assert.Equal(first.Value.DocumentId, again.Value.DocumentId);
        Assert.Equal(0m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
        Assert.Equal(received.Value.DocumentId, (await _scenario.DocumentAsync(first.Value.DocumentId)).ReversesDocumentId);
    }

    [Fact]
    public async Task FR_WH_03_reversal_fails_when_the_goods_are_gone()
    {
        var (warehouse, article) = await SetUpAsync();
        var receipt = Receipt(warehouse, article, 4m, 2m);
        ResultAssert.Succeeds(await ReceiveAsync(receipt));
        await _scenario.IssueAsync(warehouse, Line(article, 3m));

        var reversal = await ReverseAsync(new StockReversalCommand(new IdempotencyKey("purchasing-storno", receipt.Key.SourceDocumentId), _scenario.CompanyId, receipt.Key));
        var unknown = await ReverseAsync(new StockReversalCommand(new IdempotencyKey("purchasing-storno", Guid.NewGuid()), _scenario.CompanyId, new IdempotencyKey("purchasing", Guid.NewGuid())));

        ResultAssert.FailsWithCode(reversal, InsufficientStockError.ErrorCode);
        ResultAssert.Fails(unknown, StockCommandErrors.OriginalNotFound);
    }

    [Fact]
    public async Task FR_SAL_03_issue_takes_stock_at_weighted_average_once_per_key()
    {
        var (warehouse, article) = await SetUpAsync();
        ResultAssert.Succeeds(await ReceiveAsync(Receipt(warehouse, article, 2m, 1m)));
        ResultAssert.Succeeds(await ReceiveAsync(Receipt(warehouse, article, 2m, 2m)));
        var command = Issue(warehouse, article, 3m);

        var first = await IssueAsync(command);
        var again = await IssueAsync(command);

        ResultAssert.Succeeds(first);
        Assert.Equal([4.50m], first.Value.LineValues);
        Assert.Equal(first.Value.DocumentId, again.Value.DocumentId);
        Assert.Equal(1m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
        var document = await _scenario.DocumentAsync(first.Value.DocumentId);
        Assert.Equal(StockDocumentType.Issue, document.Type);
        Assert.Equal(("sales", command.Key.SourceDocumentId), (document.SourceModule, document.SourceDocumentId));
    }

    [Fact]
    public async Task FR_WH_03_issue_beyond_stock_fails_with_shortages_and_leaves_nothing_behind()
    {
        var (warehouse, article) = await SetUpAsync();
        ResultAssert.Succeeds(await ReceiveAsync(Receipt(warehouse, article, 2m, 1m)));
        var command = Issue(warehouse, article, 5m);

        var tooMuch = await IssueAsync(command);
        ResultAssert.Succeeds(await ReceiveAsync(Receipt(warehouse, article, 3m, 1m)));
        var retried = await IssueAsync(command);

        var shortage = Assert.IsType<InsufficientStockError>(tooMuch.Error);
        var missing = Assert.Single(shortage.Shortages);
        Assert.Equal((5m, 2m), (missing.Requested, missing.Available));
        ResultAssert.Succeeds(retried);
        Assert.Equal(0m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
    }

    [Fact]
    public async Task FR_SAL_05_issue_is_reversed_through_the_contract()
    {
        var (warehouse, article) = await SetUpAsync();
        ResultAssert.Succeeds(await ReceiveAsync(Receipt(warehouse, article, 4m, 2m)));
        var issue = Issue(warehouse, article, 3m);
        var issued = await IssueAsync(issue);

        var reversed = await ReverseAsync(new StockReversalCommand(new IdempotencyKey("sales", Guid.CreateVersion7()), _scenario.CompanyId, issue.Key));

        ResultAssert.Succeeds(reversed);
        Assert.Equal(issued.Value.DocumentId, (await _scenario.DocumentAsync(reversed.Value.DocumentId)).ReversesDocumentId);
        var stock = (await _scenario.StockAsync(warehouse, article))!;
        Assert.Equal((4m, 8m), (stock.Quantity, stock.Value));
    }

    private async Task<(Guid Warehouse, Guid Article)> SetUpAsync()
    {
        await _scenario.NumberSeriesAsync();
        return (await _scenario.WarehouseAsync(), await _scenario.ArticleAsync());
    }

    private StockReceiptCommand Receipt(Guid warehouse, Guid article, decimal quantity, decimal unitCost) =>
        new(new IdempotencyKey("purchasing", Guid.CreateVersion7()), _scenario.CompanyId, warehouse, _scenario.Today, "Prijem",
            [new StockReceiptCommandLine(article, quantity, unitCost, null, null, [])]);

    private Task<Result<StockDocumentReference>> ReceiveAsync(StockReceiptCommand command) =>
        _scenario.RunAsync(h => h.Commands.ReceiveAsync(command, CancellationToken.None));

    private StockIssueCommand Issue(Guid warehouse, Guid article, decimal quantity) =>
        new(new IdempotencyKey("sales", Guid.CreateVersion7()), _scenario.CompanyId, warehouse, _scenario.Today, "Otpremnica",
            [new StockIssueCommandLine(article, quantity, null, [])]);

    private Task<Result<StockDocumentReference>> IssueAsync(StockIssueCommand command) =>
        _scenario.RunAsync(h => h.Commands.IssueAsync(command, CancellationToken.None));

    private Task<Result<StockDocumentReference>> ReverseAsync(StockReversalCommand command) =>
        _scenario.RunAsync(h => h.Commands.ReverseAsync(command, CancellationToken.None));
}
