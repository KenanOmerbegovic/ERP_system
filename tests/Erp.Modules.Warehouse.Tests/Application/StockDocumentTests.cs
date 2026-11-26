using Erp.Modules.Warehouse.Contracts.StockDocuments;
using System.Text.Json;
using Erp.Messaging.Outbox;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Warehouse.Application;
using Erp.Modules.Warehouse.Application.StockDocuments;
using Erp.Modules.Warehouse.Contracts;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using static Erp.Modules.Warehouse.Tests.TestSupport.WarehouseScenario;

namespace Erp.Modules.Warehouse.Tests.Application;

/// <summary>FR-WH-01: receipts, issues and transfers; NFR-INT-01: confirmed documents are immutable and reversed by storno.</summary>
[Collection(WarehouseCollection.Name)]
public sealed class StockDocumentTests(WarehouseDatabase database)
{
    private readonly WarehouseScenario _scenario = new(database);

    [Fact]
    public async Task FR_WH_01_confirmed_receipt_increases_stock()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();

        var receiptId = await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 2.125m));

        var stock = await _scenario.StockAsync(warehouse, article);
        Assert.NotNull(stock);
        Assert.Equal(10m, stock.Quantity);
        Assert.Equal(21.25m, stock.Value);
        var receipt = await _scenario.DocumentAsync(receiptId);
        Assert.Equal(StockDocumentStatus.Confirmed, receipt.Status);
        Assert.Equal(21.25m, Assert.Single(receipt.Lines).Value);
        Assert.Equal(_scenario.Clock.UtcNow, receipt.ConfirmedAt);
    }

    [Fact]
    public async Task FR_WH_01_draft_does_not_move_stock()
    {
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();

        var draft = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 10m, unitCost: 1m));

        ResultAssert.Succeeds(draft);
        Assert.Null(await _scenario.StockAsync(warehouse, article));
        Assert.Null((await _scenario.DocumentAsync(draft.Value)).Number);
    }

    [Fact]
    public async Task FR_WH_01_issue_takes_stock_at_weighted_average_cost()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 2m));
        await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 3m));

        var issueId = await _scenario.IssueAsync(warehouse, Line(article, 4m));

        Assert.Equal(10.00m, Assert.Single((await _scenario.DocumentAsync(issueId)).Lines).Value);
        var stock = await _scenario.StockAsync(warehouse, article);
        Assert.Equal(16m, stock!.Quantity);
        Assert.Equal(40.00m, stock.Value);
        Assert.Equal(2.5m, stock.AverageCost);
    }

    [Fact]
    public async Task FR_WH_01_transfer_moves_quantity_and_value()
    {
        await _scenario.NumberSeriesAsync();
        var source = await _scenario.WarehouseAsync("MP1");
        var target = await _scenario.WarehouseAsync("MP2");
        var article = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(source, Line(article, 10m, unitCost: 2m));
        await _scenario.ReceiveAsync(target, Line(article, 10m, unitCost: 4m));

        await _scenario.PostAsync(StockDocumentType.Transfer, source, target, Line(article, 5m));

        var sourceStock = await _scenario.StockAsync(source, article);
        var targetStock = await _scenario.StockAsync(target, article);
        Assert.Equal((5m, 10.00m), (sourceStock!.Quantity, sourceStock.Value));
        Assert.Equal((15m, 50.00m), (targetStock!.Quantity, targetStock.Value));
    }

    [Fact]
    public async Task FR_WH_01_document_number_is_assigned_on_confirm_and_gapless()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var first = await _scenario.ReceiveAsync(warehouse, Line(article, 1m, unitCost: 1m));
        var failing = await _scenario.DraftAsync(StockDocumentType.Issue, warehouse, null, Line(article, 5m));
        ResultAssert.FailsWithCode(await _scenario.ConfirmAsync(failing.Value), InsufficientStockError.ErrorCode);

        var second = await _scenario.ReceiveAsync(warehouse, Line(article, 1m, unitCost: 1m));
        var issue = await _scenario.IssueAsync(warehouse, Line(article, 1m));

        Assert.Equal($"PRM-{_scenario.Today.Year}-00001", (await _scenario.DocumentAsync(first)).Number);
        Assert.Equal($"PRM-{_scenario.Today.Year}-00002", (await _scenario.DocumentAsync(second)).Number);
        Assert.Equal($"IZD-{_scenario.Today.Year}-00001", (await _scenario.DocumentAsync(issue)).Number);
    }

    [Fact]
    public async Task FR_WH_01_number_comes_from_the_series_valid_on_the_document_date()
    {
        await _scenario.NumberSeriesAsync();
        var newFormatFrom = _scenario.Today.AddDays(-1);
        ResultAssert.Succeeds(await _scenario.RunAsync(h => h.CreateNumberSeries.HandleAsync(
            new Erp.Modules.Warehouse.Application.Numbering.CreateNumberSeries(_scenario.CompanyId, StockDocumentType.Receipt, newFormatFrom, "P", 3),
            CancellationToken.None)));
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();

        var receipt = await _scenario.ReceiveAsync(warehouse, Line(article, 1m, unitCost: 1m));

        Assert.Equal($"P{_scenario.Today.Year}-001", (await _scenario.DocumentAsync(receipt)).Number);
    }

    [Fact]
    public async Task FR_WH_01_confirm_without_number_series_fails()
    {
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var draft = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 1m, unitCost: 1m));

        ResultAssert.Fails(await _scenario.ConfirmAsync(draft.Value), WarehouseErrors.NumberSeriesNotConfigured);
        Assert.Null(await _scenario.StockAsync(warehouse, article));
    }

    [Fact]
    public async Task NFR_INT_01_confirmed_document_cannot_be_edited_deleted_or_confirmed_again()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var receipt = await _scenario.ReceiveAsync(warehouse, Line(article, 1m, unitCost: 1m));

        var update = await _scenario.RunAsync(h => h.UpdateStockDocument.HandleAsync(
            new UpdateStockDocument(_scenario.CompanyId, receipt, StockDocumentType.Receipt, _scenario.Today, null, warehouse, null, [Line(article, 9m, unitCost: 1m)]),
            CancellationToken.None));
        var delete = await _scenario.RunAsync(h => h.DeleteStockDocument.HandleAsync(new DeleteStockDocument(_scenario.CompanyId, receipt), CancellationToken.None));

        ResultAssert.Fails(update, WarehouseErrors.StockDocumentNotDraft);
        ResultAssert.Fails(delete, WarehouseErrors.StockDocumentNotDraft);
        ResultAssert.Fails(await _scenario.ConfirmAsync(receipt), WarehouseErrors.StockDocumentNotDraft);
        Assert.Equal(1m, (await _scenario.StockAsync(warehouse, article))!.Quantity);
    }

    [Fact]
    public async Task FR_WH_01_draft_can_be_edited_and_deleted()
    {
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var draft = (await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 1m, unitCost: 1m))).Value;

        var update = await _scenario.RunAsync(h => h.UpdateStockDocument.HandleAsync(
            new UpdateStockDocument(_scenario.CompanyId, draft, StockDocumentType.Receipt, _scenario.Today, null, warehouse, "Dopuna",
                [Line(article, 2m, unitCost: 1m), Line(article, 3m, unitCost: 2m)]),
            CancellationToken.None));

        ResultAssert.Succeeds(update);
        var updated = await _scenario.DocumentAsync(draft);
        Assert.Equal("Dopuna", updated.Note);
        Assert.Equal([2m, 3m], updated.Lines.OrderBy(l => l.LineNumber).Select(l => l.Quantity));

        ResultAssert.Succeeds(await _scenario.RunAsync(h => h.DeleteStockDocument.HandleAsync(new DeleteStockDocument(_scenario.CompanyId, draft), CancellationToken.None)));
        Assert.False(await _scenario.QueryAsync(db => db.StockDocuments.AnyAsync(d => d.Id == draft)));
    }

    [Fact]
    public async Task NFR_INT_01_storno_reverses_receipt()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 1m));
        var wrong = await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 3m));

        var storno = await _scenario.StornoAsync(wrong);

        ResultAssert.Succeeds(storno);
        var stock = await _scenario.StockAsync(warehouse, article);
        Assert.Equal((10m, 10.00m), (stock!.Quantity, stock.Value));
        var stornoDocument = await _scenario.DocumentAsync(storno.Value);
        Assert.Equal(wrong, stornoDocument.ReversesDocumentId);
        Assert.Equal(StockDocumentStatus.Confirmed, stornoDocument.Status);
        Assert.Equal($"PRM-{_scenario.Today.Year}-00003", stornoDocument.Number);
        Assert.Equal(storno.Value, (await _scenario.DocumentAsync(wrong)).ReversedByDocumentId);
    }

    [Fact]
    public async Task NFR_INT_01_storno_of_issue_returns_goods_at_their_issue_value()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(warehouse, Line(article, 10m, unitCost: 2m));
        var issue = await _scenario.IssueAsync(warehouse, Line(article, 5m));
        await _scenario.ReceiveAsync(warehouse, Line(article, 5m, unitCost: 4m)); // average now 3.00

        ResultAssert.Succeeds(await _scenario.StornoAsync(issue));

        var stock = await _scenario.StockAsync(warehouse, article);
        Assert.Equal((15m, 40.00m), (stock!.Quantity, stock.Value)); // 10 @ 3.00 + 5 back @ 2.00
    }

    [Fact]
    public async Task NFR_INT_01_storno_of_transfer_moves_goods_back()
    {
        await _scenario.NumberSeriesAsync();
        var source = await _scenario.WarehouseAsync("MP1");
        var target = await _scenario.WarehouseAsync("MP2");
        var article = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(source, Line(article, 10m, unitCost: 2m));
        var transfer = await _scenario.PostAsync(StockDocumentType.Transfer, source, target, Line(article, 4m));

        ResultAssert.Succeeds(await _scenario.StornoAsync(transfer));

        Assert.Equal((10m, 20.00m), ((await _scenario.StockAsync(source, article))!.Quantity, (await _scenario.StockAsync(source, article))!.Value));
        Assert.Equal(0m, (await _scenario.StockAsync(target, article))!.Quantity);
    }

    [Fact]
    public async Task NFR_INT_01_document_is_reversed_only_once_and_a_storno_or_draft_is_not_reversed()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var receipt = await _scenario.ReceiveAsync(warehouse, Line(article, 1m, unitCost: 1m));
        var storno = await _scenario.StornoAsync(receipt);
        var draft = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 1m, unitCost: 1m));

        ResultAssert.Fails(await _scenario.StornoAsync(receipt), WarehouseErrors.StockDocumentAlreadyReversed);
        ResultAssert.Fails(await _scenario.StornoAsync(storno.Value), WarehouseErrors.StockDocumentIsStorno);
        ResultAssert.Fails(await _scenario.StornoAsync(draft.Value), WarehouseErrors.StockDocumentNotConfirmed);
    }

    [Fact]
    public async Task FR_WH_01_confirm_publishes_stock_events_and_storno_publishes_negative_ones()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var receipt = await _scenario.ReceiveAsync(warehouse, Line(article, 4m, unitCost: 2.5m));
        var storno = (await _scenario.StornoAsync(receipt)).Value;

        var events = await OutboxEventsAsync<StockReceived>();

        var received = Assert.Single(events, e => e.DocumentId == receipt);
        Assert.Equal(_scenario.CompanyId.Value, received.CompanyId);
        Assert.Equal(warehouse, received.WarehouseId);
        Assert.Null(received.ReversesDocumentId);
        Assert.Equal(new StockEventLine(article, null, [], 4m, 10.00m, 0m), Assert.Single(received.Lines), EventLineComparer.Instance);
        var reversed = Assert.Single(events, e => e.DocumentId == storno);
        Assert.Equal(receipt, reversed.ReversesDocumentId);
        Assert.Equal(new StockEventLine(article, null, [], -4m, -10.00m, 0m), Assert.Single(reversed.Lines), EventLineComparer.Instance);
    }

    [Fact]
    public async Task FR_WH_01_issue_and_transfer_publish_their_events()
    {
        await _scenario.NumberSeriesAsync();
        var source = await _scenario.WarehouseAsync("MP1");
        var target = await _scenario.WarehouseAsync("MP2");
        var article = await _scenario.ArticleAsync();
        await _scenario.ReceiveAsync(source, Line(article, 10m, unitCost: 1m));
        var issue = await _scenario.IssueAsync(source, Line(article, 2m));
        var transfer = await _scenario.PostAsync(StockDocumentType.Transfer, source, target, Line(article, 3m));

        var issued = Assert.Single(await OutboxEventsAsync<StockIssued>(), e => e.DocumentId == issue);
        var transferred = Assert.Single(await OutboxEventsAsync<StockTransferred>(), e => e.DocumentId == transfer);

        Assert.Equal(2.00m, Assert.Single(issued.Lines).Value);
        Assert.Equal((source, target), (transferred.SourceWarehouseId, transferred.TargetWarehouseId));
        Assert.Equal(3.00m, Assert.Single(transferred.Lines).Value);
    }

    [Fact]
    public async Task FR_WH_01_receipt_needs_unit_cost_and_issue_has_none()
    {
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();

        var receipt = await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 1m));
        var issue = await _scenario.DraftAsync(StockDocumentType.Issue, warehouse, null, Line(article, 1m, unitCost: 1m));

        ResultAssert.FailsValidation(receipt, "Lines[0].UnitCost", "NotNullValidator");
        ResultAssert.FailsValidation(issue, "Lines[0].UnitCost", WarehouseValidationCodes.NotAllowed);
    }

    [Fact]
    public async Task FR_WH_01_draft_is_validated()
    {
        var warehouse = await _scenario.WarehouseAsync();
        var inactive = await _scenario.WarehouseAsync("MP9", isActive: false);
        var article = await _scenario.ArticleAsync();
        var service = await _scenario.ArticleAsync(kind: ArticleKind.Service);

        var future = await _scenario.RunAsync(h => h.CreateStockDocument.HandleAsync(
            new CreateStockDocument(_scenario.CompanyId, StockDocumentType.Receipt, _scenario.Today.AddDays(1), null, warehouse, null, [Line(article, 1m, unitCost: 1m)]),
            CancellationToken.None));
        var sameWarehouse = await _scenario.DraftAsync(StockDocumentType.Transfer, warehouse, warehouse, Line(article, 1m));
        var noLines = await _scenario.DraftAsync(StockDocumentType.Issue, warehouse, null);
        var badQuantity = await _scenario.DraftAsync(StockDocumentType.Issue, warehouse, null, Line(article, 0m), Line(article, 1.23456m));
        var unknown = await _scenario.DraftAsync(StockDocumentType.Issue, Guid.NewGuid(), null, Line(Guid.NewGuid(), 1m));
        var notStockable = await _scenario.DraftAsync(StockDocumentType.Issue, inactive, null, Line(service, 1m));

        ResultAssert.FailsValidation(future, "DocumentDate", WarehouseValidationCodes.DocumentDateInFuture);
        ResultAssert.FailsValidation(sameWarehouse, "TargetWarehouseId", WarehouseValidationCodes.SameWarehouse);
        ResultAssert.FailsValidation(noLines, "Lines", "NotEmptyValidator");
        ResultAssert.FailsValidation(badQuantity, "Lines[0].Quantity", "GreaterThanValidator");
        ResultAssert.FailsValidation(badQuantity, "Lines[1].Quantity", "ScalePrecisionValidator");
        ResultAssert.FailsValidation(unknown, "SourceWarehouseId", WarehouseValidationCodes.WarehouseNotFound);
        ResultAssert.FailsValidation(unknown, "Lines[0].ArticleId", WarehouseValidationCodes.ArticleNotFound);
        ResultAssert.FailsValidation(notStockable, "SourceWarehouseId", WarehouseValidationCodes.WarehouseInactive);
        ResultAssert.FailsValidation(notStockable, "Lines[0].ArticleId", WarehouseValidationCodes.ArticleNotStockable);
    }

    [Fact]
    public async Task FR_WH_01_documents_of_another_company_are_out_of_reach()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync();
        var draft = (await _scenario.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 1m, unitCost: 1m))).Value;
        var other = new WarehouseScenario(database);
        await other.NumberSeriesAsync();

        ResultAssert.Fails(await other.ConfirmAsync(draft), WarehouseErrors.StockDocumentNotFound);
        ResultAssert.Fails(
            await other.RunAsync(h => h.GetStockDocument.HandleAsync(new GetStockDocument(other.CompanyId, draft), CancellationToken.None)),
            WarehouseErrors.StockDocumentNotFound);
        var otherArticle = await other.DraftAsync(StockDocumentType.Receipt, null, warehouse, Line(article, 1m, unitCost: 1m));
        ResultAssert.FailsValidation(otherArticle, "Lines[0].ArticleId", WarehouseValidationCodes.ArticleNotFound);
    }

    [Fact]
    public async Task FR_WH_01_documents_are_listed_and_read_back()
    {
        await _scenario.NumberSeriesAsync();
        var warehouse = await _scenario.WarehouseAsync();
        var article = await _scenario.ArticleAsync(code: "VIJ-M8");
        var receipt = await _scenario.ReceiveAsync(warehouse, Line(article, 2m, unitCost: 1.5m));
        await _scenario.DraftAsync(StockDocumentType.Issue, warehouse, null, Line(article, 1m));

        var confirmed = await _scenario.RunAsync(h => h.ListStockDocuments.HandleAsync(
            new ListStockDocuments(_scenario.CompanyId, null, StockDocumentStatus.Confirmed, warehouse, null, null, null, 1, 10), CancellationToken.None));
        var read = await _scenario.RunAsync(h => h.GetStockDocument.HandleAsync(new GetStockDocument(_scenario.CompanyId, receipt), CancellationToken.None));

        var summary = Assert.Single(confirmed.Value.Items);
        Assert.Equal((receipt, 1, 3.00m), (summary.Id, summary.LineCount, summary.TotalValue));
        Assert.Equal(3.00m, read.Value.TotalValue);
        Assert.Equal("VIJ-M8", Assert.Single(read.Value.Lines).ArticleCode);
    }

    private async Task<List<TEvent>> OutboxEventsAsync<TEvent>()
    {
        var type = typeof(TEvent).FullName;
        var messages = await _scenario.QueryAsync(db => db.Set<OutboxMessage>().Where(m => m.Type == type).ToListAsync());
        return messages
            .Select(m => JsonSerializer.Deserialize<TEvent>(m.Content, JsonSerializerOptions.Web)!)
            .Where(e => e is not null)
            .ToList();
    }

    /// <summary>Records compare lists by reference; event lines are compared by their content.</summary>
    private sealed class EventLineComparer : IEqualityComparer<StockEventLine>
    {
        public static readonly EventLineComparer Instance = new();

        private static readonly string[] NoSerials = [];

        public bool Equals(StockEventLine? x, StockEventLine? y) =>
            x is not null && y is not null
            && x with { SerialNumbers = NoSerials } == y with { SerialNumbers = NoSerials }
            && x.SerialNumbers.SequenceEqual(y.SerialNumbers);

        public int GetHashCode(StockEventLine obj) => obj.ArticleId.GetHashCode();
    }
}
