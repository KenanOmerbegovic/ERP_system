using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Invoices;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Contracts.Invoices;
using Erp.Modules.Sales.Tests.TestSupport;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using static Erp.Modules.Sales.Tests.TestSupport.SalesScenario;

namespace Erp.Modules.Sales.Tests.Application;

[Collection(SalesCollection.Name)]
public sealed class SalesInvoiceTests(SalesHost host)
{
    private readonly SalesScenario _scenario = new(host);

    [Fact]
    public async Task FR_SAL_03_goods_are_invoiced_up_to_what_was_delivered_and_services_from_the_order()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        var service = await _scenario.ServiceAsync();
        await _scenario.StockInAsync(warehouse, article, 10m, 1m);
        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, 5m, 10m), Line(service, 2m, 25m));
        var orderLines = await _scenario.OrderLinesAsync(order);
        var note = await _scenario.DeliverAsync(order, warehouse, Delivery(orderLines[0], 3m));
        var deliveryLine = (await _scenario.DeliveryLinesAsync(note))[0];

        var beyondDelivered = await _scenario.DraftInvoiceAsync(order, Goods(deliveryLine, 4m));
        var goodsByOrderLine = await _scenario.DraftInvoiceAsync(order, Service(orderLines[0], 1m));
        var beyondOrdered = await _scenario.DraftInvoiceAsync(order, Service(orderLines[1], 3m));
        var invoice = await _scenario.InvoiceAsync(order, Goods(deliveryLine, 3m), Service(orderLines[1], 2m));

        ResultAssert.FailsValidation(beyondDelivered, "Lines[0].Quantity", SalesValidationCodes.QuantityExceedsUninvoiced);
        ResultAssert.FailsValidation(goodsByOrderLine, "Lines[0].OrderLineId", SalesValidationCodes.GoodsNeedDelivery);
        ResultAssert.FailsValidation(beyondOrdered, "Lines[0].Quantity", SalesValidationCodes.QuantityExceedsUninvoiced);
        var issued = Assert.Single(await _scenario.EventsAsync<InvoiceIssued>());
        Assert.Equal((invoice, "IFK-2026-00001", 80m, 13.60m, 93.60m), (issued.InvoiceId, issued.Number, issued.NetAmount, issued.VatAmount, issued.GrossAmount));
        Assert.Equal([(false, (Guid?)deliveryLine), (true, null)], issued.Lines.Select(l => (l.IsService, l.DeliveryLineId)));
        var invoiceable = await _scenario.RunAsync<ListInvoiceableLinesHandler, Result<IReadOnlyList<InvoiceableLineResponse>>>(h =>
            h.HandleAsync(new ListInvoiceableLines(_scenario.CompanyId, order), CancellationToken.None));
        Assert.Empty(invoiceable.Value);
    }

    [Fact]
    public async Task FR_SAL_04_pdv_is_computed_per_line_rounded_to_fening_and_totalled_per_rate()
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var standard = await _scenario.ArticleAsync();
        var exempt = await _scenario.ArticleAsync(vatCode: "OSL");
        var service = await _scenario.ServiceAsync();
        await _scenario.StockInAsync(warehouse, standard, 10m, 1m);
        await _scenario.StockInAsync(warehouse, exempt, 10m, 1m);
        var order = await _scenario.OrderAsync(customer, warehouse, Line(standard, 3m, 3.33m), Line(exempt, 2m, 50m), Line(service, 1m, 0.10m));
        var lines = await _scenario.OrderLinesAsync(order);
        var note = await _scenario.DeliverAsync(order, warehouse, Delivery(lines[0], 3m), Delivery(lines[1], 2m));
        var delivery = await _scenario.DeliveryLinesAsync(note);

        var invoice = await _scenario.InvoiceAsync(order, Goods(delivery[0], 3m), Goods(delivery[1], 2m), Service(lines[2], 1m));

        var response = await Get(invoice);
        // 3 × 3.33 = 9.99 → PDV 1.6983 ≈ 1.70; 0.10 → PDV 0.017 ≈ 0.02; the exempt line has none.
        Assert.Equal([(9.99m, 1.70m), (100m, 0m), (0.10m, 0.02m)], response.Lines.Select(l => (l.NetAmount, l.VatAmount)));
        Assert.Equal([("PDV17", 10.09m, 1.72m), ("OSL", 100m, 0m)], response.VatTotals.Select(t => (t.VatCode, t.Base, t.VatAmount)));
        Assert.Equal((110.09m, 1.72m, 111.81m), (response.NetAmount, response.VatAmount, response.GrossAmount));
    }

    [Fact]
    public async Task FR_SAL_04_a_vat_code_without_rate_is_reported_on_its_lines()
    {
        var result = await VatApplication.CalculateAsync(
            new MissingRateCalculator(), _scenario.CompanyId, _scenario.Today, [(1, 10m, "PDV17"), (2, 5m, "X1"), (3, 7m, "X1")], CancellationToken.None);

        ResultAssert.FailsValidation(result, "Lines[1].VatCode", SalesValidationCodes.VatRateNotFound);
        ResultAssert.FailsValidation(result, "Lines[2].VatCode", SalesValidationCodes.VatRateNotFound);
        Assert.False(((ValidationError)result.Error).Errors.ContainsKey("Lines[0].VatCode"));
    }

    [Fact]
    public async Task FR_SAL_05_confirmed_invoice_cannot_be_edited()
    {
        var (order, deliveryLine) = await DeliveredAsync(2m);
        var invoice = await _scenario.InvoiceAsync(order, Goods(deliveryLine, 2m));

        var update = await _scenario.RunAsync<UpdateSalesInvoiceHandler, Result>(h => h.HandleAsync(
            new UpdateSalesInvoice(_scenario.CompanyId, invoice, order, _scenario.Today, _scenario.Today, null, [Goods(deliveryLine, 1m)]), CancellationToken.None));
        var delete = await _scenario.RunAsync<DeleteSalesInvoiceHandler, Result>(h => h.HandleAsync(new DeleteSalesInvoice(_scenario.CompanyId, invoice), CancellationToken.None));
        var again = await _scenario.ConfirmInvoiceAsync(invoice);

        ResultAssert.Fails(update, SalesErrors.InvoiceNotDraft);
        ResultAssert.Fails(delete, SalesErrors.InvoiceNotDraft);
        ResultAssert.Fails(again, SalesErrors.InvoiceNotDraft);
    }

    [Fact]
    public async Task FR_SAL_05_storno_reverses_the_whole_invoice_and_frees_its_quantities()
    {
        var (order, deliveryLine) = await DeliveredAsync(2m);
        var invoice = await _scenario.InvoiceAsync(order, Goods(deliveryLine, 2m));

        var storno = await Storno(invoice);

        ResultAssert.Succeeds(storno);
        var reversal = (await _scenario.EventsAsync<InvoiceIssued>())[^1];
        Assert.Equal((invoice, -23.40m), (reversal.ReversesInvoiceId!.Value, reversal.GrossAmount));
        Assert.Equal(SalesDocumentState.Confirmed, (await Get(storno.Value)).Status);
        await _scenario.InvoiceAsync(order, Goods(deliveryLine, 2m)); // the freed quantity can be invoiced again
        ResultAssert.Fails(await Storno(invoice), SalesErrors.AlreadyReversed);
    }

    [Fact]
    public async Task FR_SAL_05_storno_is_refused_once_a_credit_note_was_made()
    {
        var (order, deliveryLine) = await DeliveredAsync(2m);
        var invoice = await _scenario.InvoiceAsync(order, Goods(deliveryLine, 2m));
        await _scenario.CreditAsync(invoice, null, Reduction((await _scenario.InvoiceLinesAsync(invoice))[0], 1m));

        ResultAssert.Fails(await Storno(invoice), SalesErrors.InvoiceCredited);
    }

    private async Task<(Guid Order, Guid DeliveryLine)> DeliveredAsync(decimal quantity)
    {
        var (customer, warehouse) = await _scenario.SetUpAsync();
        var article = await _scenario.ArticleAsync();
        await _scenario.StockInAsync(warehouse, article, quantity, 1m);
        var order = await _scenario.OrderAsync(customer, warehouse, Line(article, quantity, 10m));
        var note = await _scenario.DeliverAsync(order, warehouse, Delivery((await _scenario.OrderLinesAsync(order))[0], quantity));
        return (order, (await _scenario.DeliveryLinesAsync(note))[0]);
    }

    private Task<Result<Guid>> Storno(Guid invoice) =>
        _scenario.RunAsync<StornoSalesInvoiceHandler, Result<Guid>>(h => h.HandleAsync(new StornoSalesInvoice(_scenario.CompanyId, invoice), CancellationToken.None));

    private async Task<SalesInvoiceResponse> Get(Guid invoice)
    {
        var result = await _scenario.RunAsync<GetSalesInvoiceHandler, Result<SalesInvoiceResponse>>(h =>
            h.HandleAsync(new GetSalesInvoice(_scenario.CompanyId, invoice), CancellationToken.None));
        ResultAssert.Succeeds(result);
        return result.Value;
    }

    private sealed class MissingRateCalculator : IVatCalculator
    {
        public Task<Result<VatCalculation>> CalculateAsync(VatCalculationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<Result<VatCalculation>>(new VatRateNotFoundError(["X1"], request.Date, ["2", "3"]));
    }
}
