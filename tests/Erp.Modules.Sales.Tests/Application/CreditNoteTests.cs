using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Sales.Application;
using Erp.Modules.Sales.Application.CreditNotes;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Tests.TestSupport;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using static Erp.Modules.Sales.Tests.TestSupport.SalesScenario;

namespace Erp.Modules.Sales.Tests.Application;

[Collection(SalesCollection.Name)]
public sealed class CreditNoteTests(SalesHost host)
{
    private readonly SalesScenario _scenario = new(host);

    [Fact]
    public async Task FR_SAL_05_returned_goods_come_back_at_their_issue_cost_and_the_customer_is_credited_at_the_invoice_price()
    {
        var setup = await InvoicedAsync(stock: [(4m, 10m), (4m, 12m)], sold: 6m, price: 20m);

        var creditNote = await _scenario.CreditAsync(setup.Invoice, setup.Warehouse, Return(setup.InvoiceLine, 2m));

        // 6 issued at the 11 KM average; 2 returned at 11 → 4 left at 44 KM.
        Assert.Equal((4m, 44m), await _scenario.StockAsync(setup.Warehouse, setup.Article));
        var issued = Assert.Single(await _scenario.EventsAsync<CreditNoteIssued>());
        Assert.Equal((creditNote, "KNO-2026-00001", 40m, 6.80m, 46.80m, 22m), (issued.CreditNoteId, issued.Number, issued.NetAmount, issued.VatAmount, issued.GrossAmount, issued.ReturnedCostValue));
        Assert.Equal((CreditNoteLineKind.Return, 2m, 22m), (issued.Lines[0].Kind, issued.Lines[0].Quantity, issued.Lines[0].ReturnedCostValue));
        var line = await _scenario.QueryAsync(db => db.SalesInvoices.SelectMany(i => i.Lines).FirstAsync(l => l.Id == setup.InvoiceLine));
        Assert.Equal((40m, 2m), (line.CreditedAmount, line.ReturnedQuantity));
    }

    [Fact]
    public async Task FR_SAL_05_value_reduction_credits_the_customer_without_moving_stock()
    {
        var setup = await InvoicedAsync(stock: [(5m, 10m)], sold: 5m, price: 20m);

        var creditNote = await _scenario.CreditAsync(setup.Invoice, null, Reduction(setup.InvoiceLine, 15.56m));

        Assert.Equal((0m, 0m), await _scenario.StockAsync(setup.Warehouse, setup.Article));
        var issued = Assert.Single(await _scenario.EventsAsync<CreditNoteIssued>());
        Assert.Equal((creditNote, 15.56m, 2.65m, (Guid?)null), (issued.CreditNoteId, issued.NetAmount, issued.VatAmount, issued.StockDocumentId));
    }

    [Fact]
    public async Task FR_SAL_05_credit_never_exceeds_what_is_left_on_the_invoice_line()
    {
        var setup = await InvoicedAsync(stock: [(5m, 10m)], sold: 3m, price: 20m);
        await _scenario.CreditAsync(setup.Invoice, setup.Warehouse, Return(setup.InvoiceLine, 1m));

        var tooMany = await _scenario.DraftCreditNoteAsync(setup.Invoice, setup.Warehouse, Return(setup.InvoiceLine, 3m));
        var tooMuch = await _scenario.DraftCreditNoteAsync(setup.Invoice, null, Reduction(setup.InvoiceLine, 40.01m));
        var noWarehouse = await _scenario.DraftCreditNoteAsync(setup.Invoice, null, Return(setup.InvoiceLine, 1m));
        var rest = await _scenario.DraftCreditNoteAsync(setup.Invoice, null, Reduction(setup.InvoiceLine, 40m));

        ResultAssert.FailsValidation(tooMany, "Lines[0].Quantity", SalesValidationCodes.QuantityExceedsReturnable);
        ResultAssert.FailsValidation(tooMuch, "Lines[0].NetAmount", SalesValidationCodes.AmountExceedsCreditable);
        ResultAssert.FailsValidation(noWarehouse, "WarehouseId", SalesValidationCodes.WarehouseRequired);
        ResultAssert.Succeeds(rest);
    }

    [Fact]
    public async Task FR_SAL_05_serial_numbers_returned_must_have_been_delivered()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync(ArticleTracking.Serial);
        await _scenario.StockInAsync(warehouse, article, 3m, 100m, null, "S-1", "S-2", "S-3");
        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, 2m, 150m));
        var note = await _scenario.DeliverAsync(order, warehouse, Delivery((await _scenario.OrderLinesAsync(order))[0], 2m, null, "S-1", "S-2"));
        var invoice = await _scenario.InvoiceAsync(order, Goods((await _scenario.DeliveryLinesAsync(note))[0], 2m));
        var invoiceLine = (await _scenario.InvoiceLinesAsync(invoice))[0];

        var notDelivered = await _scenario.DraftCreditNoteAsync(invoice, warehouse, Return(invoiceLine, 1m, "S-3"));
        await _scenario.CreditAsync(invoice, warehouse, Return(invoiceLine, 1m, "S-2"));

        ResultAssert.FailsValidation(notDelivered, "Lines[0].SerialNumbers", SalesValidationCodes.SerialNumberNotDelivered);
        Assert.Equal((2m, 200m), await _scenario.StockAsync(warehouse, article));
    }

    [Fact]
    public async Task FR_SAL_05_service_lines_can_only_be_reduced_in_value()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var service = await _scenario.ServiceAsync();
        var order = await _scenario.OrderAsync(customer, warehouse, Line(service, 1m, 100m));
        var invoice = await _scenario.InvoiceAsync(order, Service((await _scenario.OrderLinesAsync(order))[0], 1m));
        var invoiceLine = (await _scenario.InvoiceLinesAsync(invoice))[0];

        var returned = await _scenario.DraftCreditNoteAsync(invoice, warehouse, Return(invoiceLine, 1m));
        await _scenario.CreditAsync(invoice, null, Reduction(invoiceLine, 30m));

        ResultAssert.FailsValidation(returned, "Lines[0].InvoiceLineId", SalesValidationCodes.ServiceNotReturned);
    }

    [Fact]
    public async Task FR_SAL_05_storno_of_a_return_takes_the_goods_out_again_and_restores_the_invoice()
    {
        var setup = await InvoicedAsync(stock: [(5m, 10m)], sold: 4m, price: 20m);
        var creditNote = await _scenario.CreditAsync(setup.Invoice, setup.Warehouse, Return(setup.InvoiceLine, 2m));

        var storno = await Storno(creditNote);

        ResultAssert.Succeeds(storno);
        Assert.Equal((1m, 10m), await _scenario.StockAsync(setup.Warehouse, setup.Article));
        var reversal = (await _scenario.EventsAsync<CreditNoteIssued>())[^1];
        Assert.Equal((creditNote, -40m, -20m), (reversal.ReversesCreditNoteId!.Value, reversal.NetAmount, reversal.ReturnedCostValue));
        var line = await _scenario.QueryAsync(db => db.SalesInvoices.SelectMany(i => i.Lines).FirstAsync(l => l.Id == setup.InvoiceLine));
        Assert.Equal((0m, 0m), (line.CreditedAmount, line.ReturnedQuantity));
        ResultAssert.Fails(await Storno(creditNote), SalesErrors.AlreadyReversed);
    }

    [Fact]
    public async Task FR_WH_03_storno_of_a_return_fails_when_the_returned_goods_are_gone()
    {
        var setup = await InvoicedAsync(stock: [(2m, 10m)], sold: 2m, price: 20m);
        var creditNote = await _scenario.CreditAsync(setup.Invoice, setup.Warehouse, Return(setup.InvoiceLine, 2m));
        var other = await _scenario.OrderAsync(await _scenario.CustomerAsync("K002"), setup.Warehouse, Line(setup.Article, 2m, 20m));
        await _scenario.DeliverAsync(other, setup.Warehouse, Delivery((await _scenario.OrderLinesAsync(other))[0], 2m));

        var storno = await Storno(creditNote);

        ResultAssert.FailsWithCode(storno, InsufficientStockError.ErrorCode);
        var original = await _scenario.QueryAsync(db => db.CreditNotes.FirstAsync(c => c.Id == creditNote));
        Assert.Equal((DocumentStatus.Confirmed, (Guid?)null), (original.Status, original.ReversedByCreditNoteId));
        Assert.Equal(1, await _scenario.QueryAsync(db => db.CreditNotes.CountAsync(c => c.CompanyId == _scenario.CompanyId)));
    }

    private async Task<(Guid Warehouse, Guid Article, Guid Invoice, Guid InvoiceLine)> InvoicedAsync((decimal Quantity, decimal Cost)[] stock, decimal sold, decimal price)
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        foreach (var (quantity, cost) in stock)
        {
            await _scenario.StockInAsync(warehouse, article, quantity, cost);
        }

        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, sold, price));
        var note = await _scenario.DeliverAsync(order, warehouse, Delivery((await _scenario.OrderLinesAsync(order))[0], sold));
        var invoice = await _scenario.InvoiceAsync(order, Goods((await _scenario.DeliveryLinesAsync(note))[0], sold));
        return (warehouse, article, invoice, (await _scenario.InvoiceLinesAsync(invoice))[0]);
    }

    private Task<Result<Guid>> Storno(Guid creditNote) =>
        _scenario.RunAsync<StornoCreditNoteHandler, Result<Guid>>(h => h.HandleAsync(new StornoCreditNote(_scenario.CompanyId, creditNote), CancellationToken.None));
}
