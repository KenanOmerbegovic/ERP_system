using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Purchasing.Application;
using Erp.Modules.Purchasing.Application.Orders;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Tests.TestSupport;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using static Erp.Modules.Purchasing.Tests.TestSupport.PurchasingScenario;

namespace Erp.Modules.Purchasing.Tests.Application;

/// <summary>FR-PUR-01: purchase orders to suppliers; NFR-INT-01: confirmed orders are immutable and reversed by storno.</summary>
[Collection(PurchasingCollection.Name)]
public sealed class PurchaseOrderTests(PurchasingHost host)
{
    private readonly PurchasingScenario _scenario = new(host);

    [Fact]
    public async Task FR_PUR_01_confirmed_order_is_numbered_and_immutable()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();

        var orderId = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 10m, 2.5m));

        var order = await _scenario.QueryAsync(db => db.PurchaseOrders.Include(o => o.Lines).SingleAsync(o => o.Id == orderId));
        Assert.Equal(($"NAR-{_scenario.Today.Year}-00001", DocumentStatus.Confirmed, 25.00m), (order.Number, order.Status, order.NetAmount));
        Assert.Equal("PDV17", Assert.Single(order.Lines).VatCode); // the article's default
        var update = await _scenario.RunAsync<UpdatePurchaseOrderHandler, Result>(h => h.HandleAsync(
            new UpdatePurchaseOrder(_scenario.CompanyId, orderId, supplier, _scenario.Today, null, warehouse, null, [OrderLine(article, 1m, 1m)]), CancellationToken.None));
        var delete = await _scenario.RunAsync<DeletePurchaseOrderHandler, Result>(h => h.HandleAsync(new DeletePurchaseOrder(_scenario.CompanyId, orderId), CancellationToken.None));
        ResultAssert.Fails(update, PurchasingErrors.OrderNotDraft);
        ResultAssert.Fails(delete, PurchasingErrors.OrderNotDraft);
    }

    [Fact]
    public async Task FR_PUR_01_order_needs_active_supplier_goods_and_a_vat_rate()
    {
        await _scenario.NumberSeriesAsync();
        var customerOnly = await _scenario.SupplierAsync("K001", isSupplier: false);
        var supplier = await _scenario.SupplierAsync("D002");
        var service = await _scenario.ArticleAsync(kind: ArticleKind.Service, vatCode: "PDV17");
        var noDefault = await _scenario.ArticleAsync(vatCode: null);
        var goods = await _scenario.ArticleAsync();

        var wrongSupplier = await _scenario.DraftOrderAsync(customerOnly, null, OrderLine(goods, 1m, 1m));
        var lines = await _scenario.DraftOrderAsync(supplier, null,
            OrderLine(service, 1m, 1m), OrderLine(noDefault, 1m, 1m), OrderLine(goods, 1m, 1m, vatCode: "PDV99"), OrderLine(Guid.NewGuid(), 1m, 1m));
        var future = await _scenario.RunAsync<CreatePurchaseOrderHandler, Result<Guid>>(h => h.HandleAsync(
            new CreatePurchaseOrder(_scenario.CompanyId, supplier, _scenario.Today.AddDays(1), null, null, null, [OrderLine(goods, 1m, 1m)]), CancellationToken.None));

        ResultAssert.FailsValidation(wrongSupplier, "SupplierId", PurchasingValidationCodes.NotASupplier);
        ResultAssert.FailsValidation(lines, "Lines[0].ArticleId", PurchasingValidationCodes.ArticleNotOrderable);
        ResultAssert.FailsValidation(lines, "Lines[1].VatCode", PurchasingValidationCodes.VatCodeRequired);
        ResultAssert.FailsValidation(lines, "Lines[2].VatCode", PurchasingValidationCodes.VatRateNotFound);
        ResultAssert.FailsValidation(lines, "Lines[3].ArticleId", PurchasingValidationCodes.ArticleNotFound);
        ResultAssert.FailsValidation(future, "OrderDate", PurchasingValidationCodes.DateInFuture);
    }

    [Fact]
    public async Task NFR_INT_01_order_without_receipts_is_reversed_and_takes_no_receipts()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var orderId = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 5m, 1m));

        var storno = await _scenario.RunAsync<StornoPurchaseOrderHandler, Result<Guid>>(h => h.HandleAsync(new StornoPurchaseOrder(_scenario.CompanyId, orderId), CancellationToken.None));

        ResultAssert.Succeeds(storno);
        var reversed = await _scenario.QueryAsync(db => db.PurchaseOrders.SingleAsync(o => o.Id == storno.Value));
        Assert.Equal((orderId, DocumentStatus.Confirmed), (reversed.ReversesOrderId, reversed.Status));
        var line = (await _scenario.OrderLinesAsync(orderId))[0];
        ResultAssert.Fails(await _scenario.DraftReceiptAsync(orderId, warehouse, ReceiptLine(line, 1m)), PurchasingErrors.OrderNotOpen);
        ResultAssert.Fails(
            await _scenario.RunAsync<StornoPurchaseOrderHandler, Result<Guid>>(h => h.HandleAsync(new StornoPurchaseOrder(_scenario.CompanyId, orderId), CancellationToken.None)),
            PurchasingErrors.AlreadyReversed);
    }

    [Fact]
    public async Task FR_PUR_01_storno_of_order_with_receipts_is_rejected()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var orderId = await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 5m, 1m));
        await _scenario.ReceiveAsync(orderId, warehouse, ReceiptLine((await _scenario.OrderLinesAsync(orderId))[0], 1m));

        var storno = await _scenario.RunAsync<StornoPurchaseOrderHandler, Result<Guid>>(h => h.HandleAsync(new StornoPurchaseOrder(_scenario.CompanyId, orderId), CancellationToken.None));

        ResultAssert.Fails(storno, PurchasingErrors.OrderHasReceipts);
    }

    [Fact]
    public async Task FR_PUR_01_orders_are_listed_with_their_supplier()
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.OrderAsync(supplier, warehouse, OrderLine(article, 2m, 3m));
        await _scenario.DraftOrderAsync(supplier, warehouse, OrderLine(article, 1m, 1m));

        var open = await _scenario.RunAsync<ListPurchaseOrdersHandler, Result<Erp.Modules.Core.Contracts.Common.PagedResponse<Erp.Modules.Purchasing.Contracts.Orders.PurchaseOrderSummaryResponse>>>(h =>
            h.HandleAsync(new ListPurchaseOrders(_scenario.CompanyId, null, null, OpenOnly: true, null, 1, 10), CancellationToken.None));

        var summary = Assert.Single(open.Value.Items);
        Assert.Equal(("Dobavljač D001", 6.00m, false), (summary.SupplierName, summary.NetAmount, summary.FullyReceived));
    }
}
