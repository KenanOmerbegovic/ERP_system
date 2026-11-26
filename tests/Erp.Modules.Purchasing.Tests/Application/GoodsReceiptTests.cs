using System.Text.Json;
using Erp.Messaging.Outbox;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Purchasing.Application;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Receipts;
using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Tests.TestSupport;
using Erp.Modules.Warehouse.Application.StockDocuments;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Infrastructure;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Erp.Modules.Purchasing.Tests.TestSupport.PurchasingScenario;

namespace Erp.Modules.Purchasing.Tests.Application;

/// <summary>FR-PUR-01: goods receipts against orders move stock through Warehouse's idempotent command contract.</summary>
[Collection(PurchasingCollection.Name)]
public sealed class GoodsReceiptTests(PurchasingHost host)
{
    private readonly PurchasingScenario _scenario = new(host);

    [Fact]
    public async Task FR_PUR_01_receipt_moves_stock_at_order_price()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var order = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 10m, 2.125m));

        var receiptId = await _scenario.ReceiveAsync(order, warehouse, ReceiptLine((await _scenario.OrderLinesAsync(order))[0], 4m));

        Assert.Equal((4m, 8.50m), await _scenario.StockAsync(warehouse, article));
        var receipt = await _scenario.QueryAsync(db => db.GoodsReceipts.Include(r => r.Lines).SingleAsync(r => r.Id == receiptId));
        Assert.Equal(($"PRI-{_scenario.Today.Year}-00001", DocumentStatus.Confirmed, 8.50m), (receipt.Number, receipt.Status, receipt.TotalValue));
        var stockDocument = await _scenario.InScopeAsync(s => s.GetRequiredService<WarehouseDbContext>().StockDocuments.SingleAsync(d => d.Id == receipt.StockDocumentId));
        Assert.Equal(("purchasing", receiptId, StockDocumentType.Receipt), (stockDocument.SourceModule, stockDocument.SourceDocumentId, stockDocument.Type));
        var received = Assert.Single(await OutboxEventsAsync<GoodsReceived>(), e => e.ReceiptId == receiptId);
        Assert.Equal((receipt.StockDocumentId!.Value, 8.50m), (received.StockDocumentId, received.TotalValue));
    }

    [Fact]
    public async Task FR_PUR_01_partial_receipts_track_open_quantity()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var order = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 10m, 1m));
        var line = (await _scenario.OrderLinesAsync(order))[0];

        await _scenario.ReceiveAsync(order, warehouse, ReceiptLine(line, 3m));
        await _scenario.ReceiveAsync(order, warehouse, ReceiptLine(line, 7m));

        var orderLine = await _scenario.QueryAsync(db => db.PurchaseOrders.SelectMany(o => o.Lines).SingleAsync(l => l.Id == line));
        Assert.Equal((10m, 0m), (orderLine.ReceivedQuantity, orderLine.OpenQuantity));
        Assert.Equal((10m, 10.00m), await _scenario.StockAsync(warehouse, article));
    }

    [Fact]
    public async Task FR_PUR_01_receipt_beyond_open_quantity_is_rejected()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var order = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 10m, 1m));
        var line = (await _scenario.OrderLinesAsync(order))[0];
        await _scenario.ReceiveAsync(order, warehouse, ReceiptLine(line, 8m));

        var tooMuch = await _scenario.DraftReceiptAsync(order, warehouse, ReceiptLine(line, 2m), ReceiptLine(line, 1m));

        ResultAssert.FailsValidation(tooMuch, "Lines[1].Quantity", PurchasingValidationCodes.QuantityExceedsOpen);
    }

    [Fact]
    public async Task FR_PUR_01_concurrent_receipts_cannot_both_take_the_last_pieces()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var order = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 10m, 1m));
        var line = (await _scenario.OrderLinesAsync(order))[0];
        var drafts = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            drafts.Add((await _scenario.DraftReceiptAsync(order, warehouse, ReceiptLine(line, 6m))).Value);
        }

        var results = await Task.WhenAll(drafts.Select(id => Task.Run(() => _scenario.ConfirmReceiptAsync(id))));

        Assert.Single(results, r => r.IsSuccess);
        Assert.All(results.Where(r => r.IsFailure), r => ResultAssert.FailsValidation(r, "Lines[0].Quantity", PurchasingValidationCodes.QuantityExceedsOpen));
        Assert.Equal(6m, (await _scenario.StockAsync(warehouse, article)).Quantity);
    }

    [Fact]
    public async Task FR_PUR_01_same_receipt_sent_twice_moves_stock_once()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var order = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 10m, 1m));
        var draft = (await _scenario.DraftReceiptAsync(order, warehouse, ReceiptLine((await _scenario.OrderLinesAsync(order))[0], 5m))).Value;

        // The warehouse receives the goods, then the connection drops before Purchasing hears back.
        var crashing = await _scenario.InScopeAsync(async services =>
        {
            var handler = new ConfirmGoodsReceiptHandler(
                services.GetRequiredService<IPurchasingDbContext>(), services.GetRequiredService<IPurchasingLocks>(),
                new LostAnswerWarehouse(services.GetRequiredService<IStockDocumentCommands>()), services.GetRequiredService<IClock>());
            return await Record.ExceptionAsync(() => handler.HandleAsync(new ConfirmGoodsReceipt(_scenario.CompanyId, draft), CancellationToken.None));
        });
        Assert.IsType<TimeoutException>(crashing);
        Assert.Equal(DocumentStatus.Confirming, (await _scenario.QueryAsync(db => db.GoodsReceipts.SingleAsync(r => r.Id == draft))).Status);
        Assert.Equal(5m, (await _scenario.StockAsync(warehouse, article)).Quantity);

        // Confirming again sends the same receipt to the warehouse; it answers with the same stock document.
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() => _scenario.ConfirmReceiptAsync(draft))));

        Assert.All(results, ResultAssert.Succeeds);
        Assert.Equal((5m, 5.00m), await _scenario.StockAsync(warehouse, article));
        var receipt = await _scenario.QueryAsync(db => db.GoodsReceipts.SingleAsync(r => r.Id == draft));
        Assert.Equal(DocumentStatus.Confirmed, receipt.Status);
        Assert.Equal(1, await _scenario.InScopeAsync(s => s.GetRequiredService<WarehouseDbContext>().StockDocuments.CountAsync(d => d.SourceDocumentId == draft)));
        Assert.Equal(5m, await _scenario.QueryAsync(db => db.PurchaseOrders.SelectMany(o => o.Lines).Where(l => l.OrderId == order).SumAsync(l => l.ReceivedQuantity)));
        Assert.Single(await OutboxEventsAsync<GoodsReceived>(), e => e.ReceiptId == draft);
    }

    [Fact]
    public async Task FR_PUR_01_warehouse_failure_returns_receipt_to_draft()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var laptop = await _scenario.ArticleAsync(ArticleTracking.Serial);
        var order = await _scenario.OrderAsync(supplier, warehouse, OrderLine(laptop, 2m, 800m));
        var line = (await _scenario.OrderLinesAsync(order))[0];
        await _scenario.ReceiveAsync(order, warehouse, ReceiptLine(line, 1m, null, "SN-1"));
        var duplicate = (await _scenario.DraftReceiptAsync(order, warehouse, ReceiptLine(line, 1m, null, "SN-1"))).Value;

        var result = await _scenario.ConfirmReceiptAsync(duplicate);

        ResultAssert.FailsWithCode(result, "SerialNumber.AlreadyInStock");
        var receipt = await _scenario.QueryAsync(db => db.GoodsReceipts.SingleAsync(r => r.Id == duplicate));
        Assert.Equal((DocumentStatus.Draft, (string?)null), (receipt.Status, receipt.Number));
        Assert.Equal(1m, (await _scenario.StockAsync(warehouse, laptop)).Quantity);
    }

    [Fact]
    public async Task FR_PUR_01_tracking_rules_apply_before_the_warehouse_is_asked()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var flour = await _scenario.ArticleAsync(ArticleTracking.Lot);
        var laptop = await _scenario.ArticleAsync(ArticleTracking.Serial);
        var order = await _scenario.OrderAsync(supplier, warehouse, OrderLine(flour, 5m, 1m), OrderLine(laptop, 2m, 1m));
        var lines = await _scenario.OrderLinesAsync(order);

        var draft = await _scenario.DraftReceiptAsync(order, warehouse, ReceiptLine(lines[0], 5m), ReceiptLine(lines[1], 2m, null, "SN-1"));

        ResultAssert.FailsValidation(draft, "Lines[0].LotNumber", PurchasingValidationCodes.LotNumberRequired);
        ResultAssert.FailsValidation(draft, "Lines[1].SerialNumbers", PurchasingValidationCodes.SerialNumberCountMismatch);
    }

    [Fact]
    public async Task NFR_INT_01_storno_of_receipt_takes_the_goods_back_out()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var order = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 10m, 2m));
        var line = (await _scenario.OrderLinesAsync(order))[0];
        var receipt = await _scenario.ReceiveAsync(order, warehouse, ReceiptLine(line, 4m));

        var storno = await StornoAsync(receipt);

        ResultAssert.Succeeds(storno);
        Assert.Equal((0m, 0m), await _scenario.StockAsync(warehouse, article));
        var orderLine = await _scenario.QueryAsync(db => db.PurchaseOrders.SelectMany(o => o.Lines).SingleAsync(l => l.Id == line));
        Assert.Equal(0m, orderLine.ReceivedQuantity);
        var reversed = Assert.Single(await OutboxEventsAsync<GoodsReceived>(), e => e.ReceiptId == storno.Value);
        Assert.Equal((receipt, -4m, -8.00m), (reversed.ReversesReceiptId, reversed.Lines[0].Quantity, reversed.TotalValue));
        ResultAssert.Fails(await StornoAsync(receipt), PurchasingErrors.AlreadyReversed);
    }

    [Fact]
    public async Task FR_WH_03_storno_of_receipt_whose_goods_left_is_rejected_and_undone()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var order = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 10m, 2m));
        var receipt = await _scenario.ReceiveAsync(order, warehouse, ReceiptLine((await _scenario.OrderLinesAsync(order))[0], 4m));
        await IssueAsync(warehouse, article, 3m);

        var storno = await StornoAsync(receipt);

        var error = Assert.IsType<InsufficientStockError>(storno.Error);
        Assert.Equal((4m, 1m), (error.Shortages[0].Requested, error.Shortages[0].Available));
        var original = await _scenario.QueryAsync(db => db.GoodsReceipts.SingleAsync(r => r.Id == receipt));
        Assert.Null(original.ReversedByReceiptId);
        Assert.Equal(1, await _scenario.QueryAsync(db => db.GoodsReceipts.CountAsync(r => r.PurchaseOrderId == order)));
    }

    private Task<Result<Guid>> StornoAsync(Guid receipt) =>
        _scenario.RunAsync<StornoGoodsReceiptHandler, Result<Guid>>(h => h.HandleAsync(new StornoGoodsReceipt(_scenario.CompanyId, receipt), CancellationToken.None));

    private async Task IssueAsync(Guid warehouse, Guid article, decimal quantity)
    {
        var draft = await _scenario.RunAsync<CreateStockDocumentHandler, Result<Guid>>(h => h.HandleAsync(
            new CreateStockDocument(_scenario.CompanyId, StockDocumentType.Issue, _scenario.Today, warehouse, null, null,
                [new StockLineInput(article, quantity, null, null, null, [])]),
            CancellationToken.None));
        ResultAssert.Succeeds(await _scenario.RunAsync<ConfirmStockDocumentHandler, Result>(h =>
            h.HandleAsync(new ConfirmStockDocument(_scenario.CompanyId, draft.Value), CancellationToken.None)));
    }

    private async Task<List<TEvent>> OutboxEventsAsync<TEvent>()
    {
        var type = typeof(TEvent).FullName;
        var messages = await _scenario.QueryAsync(db => db.Set<OutboxMessage>().Where(m => m.Type == type).ToListAsync());
        return messages.Select(m => JsonSerializer.Deserialize<TEvent>(m.Content, JsonSerializerOptions.Web)!).ToList();
    }

    /// <summary>Passes the command on, then loses the answer: the warehouse did its work, the caller never hears of it.</summary>
    private sealed class LostAnswerWarehouse(IStockDocumentCommands inner) : IStockDocumentCommands
    {
        public async Task<Result<StockDocumentReference>> ReceiveAsync(StockReceiptCommand command, CancellationToken cancellationToken)
        {
            await inner.ReceiveAsync(command, cancellationToken);
            throw new TimeoutException("The answer was lost.");
        }

        public Task<Result<StockDocumentReference>> IssueAsync(StockIssueCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<StockDocumentReference>> ReverseAsync(StockReversalCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
