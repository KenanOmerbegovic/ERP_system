using Erp.Modules.Sales.Application;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Tests.TestSupport;
using Erp.SharedKernel.Results;
using static Erp.Modules.Sales.Tests.TestSupport.SalesScenario;

namespace Erp.Modules.Sales.Tests.Application;

[Collection(SalesCollection.Name)]
public sealed class SalesOrderTests(SalesHost host)
{
    private readonly SalesScenario _scenario = new(host);

    [Fact]
    public async Task FR_SAL_03_confirming_numbers_the_order_and_publishes_it()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();

        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, 4m, 2.555m));

        var published = Assert.Single(await _scenario.EventsAsync<SalesOrderConfirmed>());
        Assert.Equal((order, "NRK-2026-00001", customer, 10.22m), (published.OrderId, published.Number, published.CustomerId, published.NetAmount));
        Assert.Equal((4m, "PDV17", false), (published.Lines[0].Quantity, published.Lines[0].VatCode, published.Lines[0].IsService));
    }

    [Fact]
    public async Task FR_SAL_05_confirmed_order_cannot_be_edited_or_deleted()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, 1m, 10m));

        var update = await _scenario.RunAsync<UpdateSalesOrderHandler, Result>(h => h.HandleAsync(
            new UpdateSalesOrder(_scenario.CompanyId, order, customer, _scenario.Today, warehouse, null, [Line(article, 2m, 10m)]), CancellationToken.None));
        var delete = await _scenario.RunAsync<DeleteSalesOrderHandler, Result>(h => h.HandleAsync(new DeleteSalesOrder(_scenario.CompanyId, order), CancellationToken.None));
        var again = await _scenario.ConfirmOrderAsync(order);

        ResultAssert.Fails(update, SalesErrors.OrderNotDraft);
        ResultAssert.Fails(delete, SalesErrors.OrderNotDraft);
        ResultAssert.Fails(again, SalesErrors.OrderNotDraft);
    }

    [Fact]
    public async Task FR_SAL_05_storno_reverses_an_order_nothing_was_delivered_on_and_closes_it()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.StockInAsync(warehouse, article, 10m, 1m);
        var untouched = await _scenario.OrderAsync(customer, warehouse, Line(article, 2m, 10m));
        var delivered = await _scenario.OrderAsync(customer, warehouse, Line(article, 2m, 10m));
        await _scenario.DeliverAsync(delivered, warehouse, Delivery((await _scenario.OrderLinesAsync(delivered))[0], 1m));

        var storno = await Storno(untouched);
        var refused = await Storno(delivered);
        var twice = await Storno(untouched);
        var ofStorno = await Storno(storno.Value);

        ResultAssert.Succeeds(storno);
        ResultAssert.Fails(refused, SalesErrors.OrderHasDeliveries);
        ResultAssert.Fails(twice, SalesErrors.AlreadyReversed);
        ResultAssert.Fails(ofStorno, SalesErrors.IsStorno);
        var events = await _scenario.EventsAsync<SalesOrderConfirmed>();
        Assert.Equal((untouched, -20m, -2m), (events[^1].ReversesOrderId!.Value, events[^1].NetAmount, events[^1].Lines[0].Quantity));
        ResultAssert.Fails(await _scenario.DraftDeliveryAsync(untouched, warehouse, Delivery((await _scenario.OrderLinesAsync(untouched))[0], 1m)), SalesErrors.OrderNotOpen);
    }

    private Task<Result<Guid>> Storno(Guid order) =>
        _scenario.RunAsync<StornoSalesOrderHandler, Result<Guid>>(h => h.HandleAsync(new StornoSalesOrder(_scenario.CompanyId, order), CancellationToken.None));
}
