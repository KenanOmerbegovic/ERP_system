using System.Text.Json;
using Erp.Messaging.Outbox;
using Erp.Modules.Purchasing.Application;
using Erp.Modules.Purchasing.Application.Invoices;
using Erp.Modules.Purchasing.Application.Receipts;
using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Purchasing.Contracts.Invoices;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Tests.TestSupport;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using static Erp.Modules.Purchasing.Tests.TestSupport.PurchasingScenario;

namespace Erp.Modules.Purchasing.Tests.Application;

/// <summary>FR-PUR-02: supplier invoices linked to purchase orders and their receipts, with input PDV.</summary>
[Collection(PurchasingCollection.Name)]
public sealed class SupplierInvoiceTests(PurchasingHost host)
{
    private readonly PurchasingScenario _scenario = new(host);

    [Fact]
    public async Task FR_PUR_02_invoice_links_order_and_receipts_and_computes_pdv_per_rate()
    {
        var (order, receiptLines) = await ReceivedAsync((10m, 2m, null), (4m, 5m, "OSL"));

        var draft = await _scenario.DraftInvoiceAsync(order, "F-1001",
            new SupplierInvoiceDraftLine(receiptLines[0], 10m, 2m, null), new SupplierInvoiceDraftLine(receiptLines[1], 4m, 5m, null));
        ResultAssert.Succeeds(draft);
        ResultAssert.Succeeds(await _scenario.ConfirmInvoiceAsync(draft.Value));

        var invoice = await GetAsync(draft.Value);
        Assert.Equal(($"ULF-{_scenario.Today.Year}-00001", PurchaseDocumentStateConfirmed, order), (invoice.Number, invoice.Status, invoice.PurchaseOrderId));
        Assert.Equal((40.00m, 3.40m, 43.40m), (invoice.NetAmount, invoice.VatAmount, invoice.GrossAmount));
        Assert.Equal([("PDV17", 20.00m, 3.40m), ("OSL", 20.00m, 0m)], invoice.VatTotals.Select(t => (t.VatCode, t.Base, t.VatAmount)));
        var orderLines = await _scenario.QueryAsync(db => db.PurchaseOrders.SelectMany(o => o.Lines).Where(l => l.OrderId == order).OrderBy(l => l.LineNumber).ToListAsync());
        Assert.Equal([10m, 4m], orderLines.Select(l => l.InvoicedQuantity));
    }

    [Fact]
    public async Task FR_PUR_02_invoice_beyond_received_quantity_is_rejected()
    {
        var (order, receiptLines) = await ReceivedAsync((10m, 2m, null));
        var first = await _scenario.DraftInvoiceAsync(order, "F-1", new SupplierInvoiceDraftLine(receiptLines[0], 8m, 2m, null));
        ResultAssert.Succeeds(await _scenario.ConfirmInvoiceAsync(first.Value));

        var tooMuch = await _scenario.DraftInvoiceAsync(order, "F-2", new SupplierInvoiceDraftLine(receiptLines[0], 3m, 2m, null));
        var unknown = await _scenario.DraftInvoiceAsync(order, "F-3", new SupplierInvoiceDraftLine(Guid.NewGuid(), 1m, 2m, null));

        ResultAssert.FailsValidation(tooMuch, "Lines[0].Quantity", PurchasingValidationCodes.QuantityExceedsUninvoiced);
        ResultAssert.FailsValidation(unknown, "Lines[0].ReceiptLineId", PurchasingValidationCodes.ReceiptLineNotFound);
    }

    [Fact]
    public async Task FR_PUR_02_two_drafts_cannot_both_invoice_the_same_goods()
    {
        var (order, receiptLines) = await ReceivedAsync((10m, 2m, null));
        var first = await _scenario.DraftInvoiceAsync(order, "F-1", new SupplierInvoiceDraftLine(receiptLines[0], 6m, 2m, null));
        var second = await _scenario.DraftInvoiceAsync(order, "F-2", new SupplierInvoiceDraftLine(receiptLines[0], 6m, 2m, null));

        var results = await Task.WhenAll(Task.Run(() => _scenario.ConfirmInvoiceAsync(first.Value)), Task.Run(() => _scenario.ConfirmInvoiceAsync(second.Value)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure);
    }

    [Fact]
    public async Task FR_PUR_02_price_difference_is_reported_and_stock_is_not_revalued()
    {
        var (order, receiptLines) = await ReceivedAsync((10m, 2m, null));
        var draft = await _scenario.DraftInvoiceAsync(order, "F-7", new SupplierInvoiceDraftLine(receiptLines[0], 10m, 2.15m, null));
        ResultAssert.Succeeds(await _scenario.ConfirmInvoiceAsync(draft.Value));

        var invoice = await GetAsync(draft.Value);
        var line = Assert.Single(invoice.Lines);
        Assert.Equal((21.50m, 20.00m, 1.50m), (line.NetAmount, line.ReceivedValue, line.PriceDifference));
        Assert.Equal(1.50m, invoice.PriceDifference);
        var registered = Assert.Single(await OutboxEventsAsync<SupplierInvoiceRegistered>(), e => e.InvoiceId == draft.Value);
        Assert.Equal((1.50m, 25.16m), (registered.Lines[0].PriceDifference, registered.GrossAmount)); // 21.50 + 17 % = 25.155 → 3.66 PDV
        Assert.Equal(20.00m, (await _scenario.StockAsync(await WarehouseOfAsync(order), await ArticleOfAsync(order))).Value);
    }

    [Fact]
    public async Task FR_PUR_02_supplier_invoice_number_is_unique_per_supplier()
    {
        var (order, receiptLines) = await ReceivedAsync((10m, 2m, null));
        var first = await _scenario.DraftInvoiceAsync(order, "F-55", new SupplierInvoiceDraftLine(receiptLines[0], 5m, 2m, null));
        ResultAssert.Succeeds(await _scenario.ConfirmInvoiceAsync(first.Value));

        var again = await _scenario.DraftInvoiceAsync(order, " F-55 ", new SupplierInvoiceDraftLine(receiptLines[0], 5m, 2m, null));

        ResultAssert.Fails(again, PurchasingErrors.DuplicateSupplierInvoiceNumber);
    }

    [Fact]
    public async Task NFR_INT_01_storno_of_invoice_releases_its_quantities_and_number()
    {
        var (order, receiptLines) = await ReceivedAsync((10m, 2m, null));
        var draft = await _scenario.DraftInvoiceAsync(order, "F-9", new SupplierInvoiceDraftLine(receiptLines[0], 10m, 2m, null));
        ResultAssert.Succeeds(await _scenario.ConfirmInvoiceAsync(draft.Value));

        var storno = await _scenario.RunAsync<StornoSupplierInvoiceHandler, Result<Guid>>(h => h.HandleAsync(new StornoSupplierInvoice(_scenario.CompanyId, draft.Value), CancellationToken.None));

        ResultAssert.Succeeds(storno);
        var reversed = Assert.Single(await OutboxEventsAsync<SupplierInvoiceRegistered>(), e => e.InvoiceId == storno.Value);
        Assert.Equal((draft.Value, -23.40m), (reversed.ReversesInvoiceId, reversed.GrossAmount));
        var corrected = await _scenario.DraftInvoiceAsync(order, "F-9", new SupplierInvoiceDraftLine(receiptLines[0], 10m, 2m, null));
        ResultAssert.Succeeds(corrected);
        ResultAssert.Succeeds(await _scenario.ConfirmInvoiceAsync(corrected.Value));
    }

    [Fact]
    public async Task FR_PUR_02_invoiced_receipt_cannot_be_reversed()
    {
        var (order, receiptLines) = await ReceivedAsync((10m, 2m, null));
        var draft = await _scenario.DraftInvoiceAsync(order, "F-10", new SupplierInvoiceDraftLine(receiptLines[0], 1m, 2m, null));
        ResultAssert.Succeeds(await _scenario.ConfirmInvoiceAsync(draft.Value));
        var receiptId = await _scenario.QueryAsync(db => db.GoodsReceipts.Where(r => r.PurchaseOrderId == order).Select(r => r.Id).SingleAsync());

        var storno = await _scenario.RunAsync<StornoGoodsReceiptHandler, Result<Guid>>(h => h.HandleAsync(new StornoGoodsReceipt(_scenario.CompanyId, receiptId), CancellationToken.None));

        ResultAssert.Fails(storno, PurchasingErrors.ReceiptInvoiced);
    }

    [Fact]
    public async Task FR_PUR_02_invoiceable_lines_show_what_is_left_to_invoice()
    {
        var (order, receiptLines) = await ReceivedAsync((10m, 2m, null));
        var draft = await _scenario.DraftInvoiceAsync(order, "F-11", new SupplierInvoiceDraftLine(receiptLines[0], 4m, 2m, null));
        ResultAssert.Succeeds(await _scenario.ConfirmInvoiceAsync(draft.Value));

        var lines = await _scenario.RunAsync<ListInvoiceableReceiptLinesHandler, Result<IReadOnlyList<InvoiceableReceiptLineResponse>>>(h =>
            h.HandleAsync(new ListInvoiceableReceiptLines(_scenario.CompanyId, order), CancellationToken.None));

        var line = Assert.Single(lines.Value);
        Assert.Equal((10m, 4m, 2m, "PDV17"), (line.ReceivedQuantity, line.InvoicedQuantity, line.UnitCost, line.VatCode));
    }

    private const Contracts.PurchaseDocumentState PurchaseDocumentStateConfirmed = Contracts.PurchaseDocumentState.Confirmed;

    /// <summary>An order with one line per (quantity, price, VAT code), fully received in one receipt.</summary>
    private async Task<(Guid Order, IReadOnlyList<Guid> ReceiptLines)> ReceivedAsync(params (decimal Quantity, decimal Price, string? VatCode)[] lines)
    {
        var (supplier, warehouse) = await _scenario.SetUpAsync();
        var orderLines = new List<Erp.Modules.Purchasing.Application.Orders.PurchaseOrderDraftLine>();
        foreach (var (quantity, price, vatCode) in lines)
        {
            orderLines.Add(OrderLine(await _scenario.ArticleAsync(), quantity, price, vatCode));
        }

        var order = await _scenario.OrderAsync(supplier, warehouse, [.. orderLines]);
        var orderLineIds = await _scenario.OrderLinesAsync(order);
        var receipt = await _scenario.ReceiveAsync(order, warehouse, [.. orderLineIds.Select((id, index) => ReceiptLine(id, lines[index].Quantity))]);
        return (order, await _scenario.ReceiptLinesAsync(receipt));
    }

    private Task<SupplierInvoiceResponse> GetAsync(Guid invoice) =>
        _scenario.RunAsync<GetSupplierInvoiceHandler, SupplierInvoiceResponse>(async h =>
            (await h.HandleAsync(new GetSupplierInvoice(_scenario.CompanyId, invoice), CancellationToken.None)).Value);

    private Task<Guid> WarehouseOfAsync(Guid order) =>
        _scenario.QueryAsync(db => db.GoodsReceipts.Where(r => r.PurchaseOrderId == order).Select(r => r.WarehouseId).FirstAsync());

    private Task<Guid> ArticleOfAsync(Guid order) =>
        _scenario.QueryAsync(db => db.PurchaseOrders.SelectMany(o => o.Lines).Where(l => l.OrderId == order).Select(l => l.ArticleId).FirstAsync());

    private async Task<List<TEvent>> OutboxEventsAsync<TEvent>()
    {
        var type = typeof(TEvent).FullName;
        var messages = await _scenario.QueryAsync(db => db.Set<OutboxMessage>().Where(m => m.Type == type).ToListAsync());
        return messages.Select(m => JsonSerializer.Deserialize<TEvent>(m.Content, JsonSerializerOptions.Web)!).ToList();
    }
}
