using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Sales.Application;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Deliveries;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Tests.TestSupport;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Erp.Modules.Sales.Tests.TestSupport.SalesScenario;

namespace Erp.Modules.Sales.Tests.Application;

[Collection(SalesCollection.Name)]
public sealed class DeliveryNoteTests(SalesHost host)
{
    private readonly SalesScenario _scenario = new(host);

    [Fact]
    public async Task FR_SAL_03_partial_deliveries_issue_stock_at_average_cost_and_publish_cost_of_goods_sold()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.StockInAsync(warehouse, article, 4m, 10m);
        await _scenario.StockInAsync(warehouse, article, 4m, 12m);
        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, 6m, 20m));
        var orderLine = (await _scenario.OrderLinesAsync(order))[0];

        var first = await _scenario.DeliverAsync(order, warehouse, Delivery(orderLine, 2m));
        var second = await _scenario.DeliverAsync(order, warehouse, Delivery(orderLine, 4m));

        Assert.Equal((2m, 22m), await _scenario.StockAsync(warehouse, article));
        var delivered = await _scenario.EventsAsync<GoodsDelivered>();
        Assert.Equal([(first, "OTP-2026-00001", 22m), (second, "OTP-2026-00002", 44m)], delivered.Select(e => (e.DeliveryNoteId, e.Number, e.CostValue)));
        Assert.Equal((orderLine, 2m, 22m), (delivered[0].Lines[0].OrderLineId, delivered[0].Lines[0].Quantity, delivered[0].Lines[0].CostValue));
        var line = await _scenario.QueryAsync(db => db.SalesOrders.SelectMany(o => o.Lines).FirstAsync(l => l.Id == orderLine));
        Assert.Equal((6m, 0m), (line.DeliveredQuantity, line.OpenQuantity));
    }

    [Fact]
    public async Task FR_SAL_03_cannot_deliver_more_than_is_open_or_a_service()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var service = await _scenario.ServiceAsync();
        await _scenario.StockInAsync(warehouse, article, 10m, 1m);
        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, 3m, 5m), Line(service, 1m, 30m));
        var lines = await _scenario.OrderLinesAsync(order);
        await _scenario.DeliverAsync(order, warehouse, Delivery(lines[0], 2m));

        var tooMuch = await _scenario.DraftDeliveryAsync(order, warehouse, Delivery(lines[0], 1m), Delivery(lines[0], 1m));
        var serviceLine = await _scenario.DraftDeliveryAsync(order, warehouse, Delivery(lines[1], 1m));

        ResultAssert.FailsValidation(tooMuch, "Lines[1].Quantity", SalesValidationCodes.QuantityExceedsOpen);
        ResultAssert.FailsValidation(serviceLine, "Lines[0].OrderLineId", SalesValidationCodes.ServiceNotDelivered);
    }

    [Fact]
    public async Task FR_WH_03_delivery_with_insufficient_stock_stays_draft_with_shortages()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.StockInAsync(warehouse, article, 2m, 5m);
        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, 5m, 9m));
        var note = (await _scenario.DraftDeliveryAsync(order, warehouse, Delivery((await _scenario.OrderLinesAsync(order))[0], 5m))).Value;

        var confirmed = await _scenario.ConfirmDeliveryAsync(note);

        var insufficient = Assert.IsType<InsufficientStockError>(confirmed.Error);
        var shortage = Assert.Single(insufficient.Shortages);
        Assert.Equal((article, 5m, 2m), (shortage.ArticleId, shortage.Requested, shortage.Available));
        Assert.Equal(DocumentStatus.Draft, await _scenario.QueryAsync(db => db.DeliveryNotes.Where(d => d.Id == note).Select(d => d.Status).FirstAsync()));
        Assert.Equal((2m, 10m), await _scenario.StockAsync(warehouse, article));
        Assert.Empty(await _scenario.EventsAsync<GoodsDelivered>());

        await _scenario.StockInAsync(warehouse, article, 3m, 5m);
        ResultAssert.Succeeds(await _scenario.ConfirmDeliveryAsync(note));
        Assert.Equal((0m, 0m), await _scenario.StockAsync(warehouse, article));
    }

    [Fact]
    public async Task FR_SAL_03_tracked_goods_need_their_lot_or_serial_numbers()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var lot = await _scenario.ArticleAsync(ArticleTracking.Lot);
        var serial = await _scenario.ArticleAsync(ArticleTracking.Serial);
        await _scenario.StockInAsync(warehouse, lot, 5m, 1m, "L-1");
        await _scenario.StockInAsync(warehouse, serial, 2m, 100m, null, "S-1", "S-2");
        var order = await _scenario.OrderAsync(customer, warehouse, Line(lot, 2m, 3m), Line(serial, 1m, 150m));
        var lines = await _scenario.OrderLinesAsync(order);

        var missing = await _scenario.DraftDeliveryAsync(order, warehouse, Delivery(lines[0], 2m), Delivery(lines[1], 1m));
        var note = await _scenario.DeliverAsync(order, warehouse, Delivery(lines[0], 2m, "L-1"), Delivery(lines[1], 1m, null, "S-2"));

        ResultAssert.FailsValidation(missing, "Lines[0].LotNumber", SalesValidationCodes.LotNumberRequired);
        ResultAssert.FailsValidation(missing, "Lines[1].SerialNumbers", SalesValidationCodes.SerialNumberCountMismatch);
        Assert.Equal((1m, 100m), await _scenario.StockAsync(warehouse, serial));
        Assert.Equal(100m, (await _scenario.EventsAsync<GoodsDelivered>()).Single(e => e.DeliveryNoteId == note).Lines[1].CostValue);
    }

    [Fact]
    public async Task FR_SAL_03_confirming_again_after_a_lost_answer_moves_stock_once()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.StockInAsync(warehouse, article, 5m, 2m);
        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, 3m, 9m));
        var note = (await _scenario.DraftDeliveryAsync(order, warehouse, Delivery((await _scenario.OrderLinesAsync(order))[0], 3m))).Value;

        var crashing = await _scenario.InScopeAsync(async services =>
        {
            var handler = new ConfirmDeliveryNoteHandler(
                services.GetRequiredService<ISalesDbContext>(), services.GetRequiredService<ISalesLocks>(),
                new LostAnswerWarehouse(services.GetRequiredService<IStockDocumentCommands>()), services.GetRequiredService<IClock>());
            return await Record.ExceptionAsync(() => handler.HandleAsync(new ConfirmDeliveryNote(_scenario.CompanyId, note), CancellationToken.None));
        });
        Assert.IsType<TimeoutException>(crashing);
        Assert.Equal(DocumentStatus.Confirming, await _scenario.QueryAsync(db => db.DeliveryNotes.Where(d => d.Id == note).Select(d => d.Status).FirstAsync()));

        ResultAssert.Succeeds(await _scenario.ConfirmDeliveryAsync(note));

        Assert.Equal((2m, 4m), await _scenario.StockAsync(warehouse, article));
        Assert.Single(await _scenario.EventsAsync<GoodsDelivered>());
    }

    [Fact]
    public async Task FR_SAL_05_storno_brings_the_goods_back_unless_the_note_is_invoiced()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.StockInAsync(warehouse, article, 10m, 3m);
        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, 6m, 9m));
        var orderLine = (await _scenario.OrderLinesAsync(order))[0];
        var reversible = await _scenario.DeliverAsync(order, warehouse, Delivery(orderLine, 2m));
        var invoiced = await _scenario.DeliverAsync(order, warehouse, Delivery(orderLine, 3m));
        await _scenario.InvoiceAsync(order, Goods((await _scenario.DeliveryLinesAsync(invoiced))[0], 1m));

        var storno = await Storno(reversible);
        var refused = await Storno(invoiced);

        ResultAssert.Succeeds(storno);
        ResultAssert.Fails(refused, SalesErrors.DeliveryNoteInvoiced);
        Assert.Equal((7m, 21m), await _scenario.StockAsync(warehouse, article));
        var reversal = (await _scenario.EventsAsync<GoodsDelivered>())[^1];
        Assert.Equal((reversible, -6m, -2m), (reversal.ReversesDeliveryNoteId!.Value, reversal.CostValue, reversal.Lines[0].Quantity));
        var line = await _scenario.QueryAsync(db => db.SalesOrders.SelectMany(o => o.Lines).FirstAsync(l => l.Id == orderLine));
        Assert.Equal(3m, line.DeliveredQuantity);
    }

    private Task<Result<Guid>> Storno(Guid note) =>
        _scenario.RunAsync<StornoDeliveryNoteHandler, Result<Guid>>(h => h.HandleAsync(new StornoDeliveryNote(_scenario.CompanyId, note), CancellationToken.None));

    /// <summary>Passes the command on, then loses the answer: the warehouse did its work, the caller never hears of it.</summary>
    private sealed class LostAnswerWarehouse(IStockDocumentCommands inner) : IStockDocumentCommands
    {
        public Task<Result<StockDocumentReference>> ReceiveAsync(StockReceiptCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<Result<StockDocumentReference>> IssueAsync(StockIssueCommand command, CancellationToken cancellationToken)
        {
            await inner.IssueAsync(command, cancellationToken);
            throw new TimeoutException("The answer was lost.");
        }

        public Task<Result<StockDocumentReference>> ReverseAsync(StockReversalCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
