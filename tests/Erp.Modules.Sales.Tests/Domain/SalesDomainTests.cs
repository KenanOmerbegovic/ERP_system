using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Invoices;
using Erp.Modules.Sales.Domain.Offers;
using Erp.Modules.Sales.Domain.Orders;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Sales.Tests.Domain;

public sealed class SalesDomainTests
{
    private static readonly CompanyId Company = CompanyId.New();
    private static readonly DateOnly Date = new(2026, 11, 2);
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Article = Guid.NewGuid();

    [Fact]
    public void FR_SAL_01_sent_offer_cannot_be_edited_and_is_decided_once()
    {
        var offer = Offer.CreateDraft(Company, Customer, Date, Date.AddDays(10), null, [new SalesLineInput(Article, false, 2m, 3.335m, "PDV17")], Now);
        offer.Send("PON-1", Now);

        Assert.Throws<InvalidOperationException>(() => offer.UpdateDraft(Customer, Date, null, null, [new SalesLineInput(Article, false, 1m, 1m, "PDV17")]));
        offer.Reject(Now);
        Assert.Throws<InvalidOperationException>(() => offer.Accept(Guid.NewGuid(), Now));
        Assert.Equal(6.67m, offer.NetAmount);
    }

    [Fact]
    public void FR_SAL_05_confirmed_order_is_immutable()
    {
        var order = SalesOrder.CreateDraft(Company, Customer, Date, null, null, [new SalesLineInput(Article, false, 1m, 10m, "PDV17")], Now);
        order.Confirm("NRK-1", Now);

        Assert.Throws<InvalidOperationException>(() => order.UpdateDraft(Customer, Date, null, null, [new SalesLineInput(Article, false, 2m, 10m, "PDV17")]));
        Assert.Throws<InvalidOperationException>(() => order.Confirm("NRK-2", Now));
    }

    [Fact]
    public void FR_SAL_03_goods_are_invoiced_up_to_delivered_and_services_up_to_ordered()
    {
        var order = SalesOrder.CreateDraft(
            Company, Customer, Date, null, null, [new SalesLineInput(Article, false, 5m, 10m, "PDV17"), new SalesLineInput(Guid.NewGuid(), true, 2m, 30m, "PDV17")], Now);
        order.Confirm("NRK-1", Now);
        var goods = order.Lines[0];
        var service = order.Lines[1];

        order.RecordDelivered(goods.Id, 3m);
        order.RecordInvoiced(service.Id, 2m);

        Assert.Equal((2m, 3m), (goods.OpenQuantity, goods.InvoiceableQuantity));
        Assert.Equal((0m, 0m), (service.OpenQuantity, service.InvoiceableQuantity));
        Assert.Throws<InvalidOperationException>(() => order.RecordInvoiced(goods.Id, 4m));
        Assert.Throws<InvalidOperationException>(() => order.RecordDelivered(service.Id, 1m));
        Assert.False(order.CanBeReversed);
    }

    [Fact]
    public void FR_SAL_05_confirmed_invoice_cannot_be_edited_and_storno_mirrors_it()
    {
        var invoice = SalesInvoice.CreateDraft(
            Company, Guid.NewGuid(), Customer, Date, Date.AddDays(15), null,
            [new SalesInvoiceLineInput(Guid.NewGuid(), null, null, Article, true, 1m, 100m, "PDV17")], Now);
        invoice.ApplyVat(new Dictionary<int, VatAmounts> { [1] = new("PDV17", 0.17m, 100m, 17m) }, [new VatAmounts("PDV17", 0.17m, 100m, 17m)]);
        invoice.Confirm("IFK-1", Now);

        Assert.Throws<InvalidOperationException>(() => invoice.UpdateDraft(Date, Date, null, []));
        Assert.Throws<InvalidOperationException>(() => invoice.ApplyVat(new Dictionary<int, VatAmounts>(), []));
        var storno = invoice.CreateStorno(Date.AddDays(1), Now);
        Assert.Equal((invoice.Id, 117m, 17m), (storno.ReversesInvoiceId!.Value, storno.GrossAmount, storno.Lines[0].VatAmount));
        Assert.False(invoice.CanBeReversed);
    }

    [Fact]
    public void FR_SAL_05_credited_invoice_cannot_be_reversed_and_credit_is_capped()
    {
        var invoice = SalesInvoice.CreateDraft(
            Company, Guid.NewGuid(), Customer, Date, Date, null, [new SalesInvoiceLineInput(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Article, false, 2m, 10m, "PDV17")], Now);
        invoice.ApplyVat(new Dictionary<int, VatAmounts> { [1] = new("PDV17", 0.17m, 20m, 3.4m) }, [new VatAmounts("PDV17", 0.17m, 20m, 3.4m)]);
        invoice.Confirm("IFK-1", Now);
        var line = invoice.Lines[0];

        line.AddCredit(5m, 0m);

        Assert.False(invoice.CanBeReversed);
        Assert.Equal((15m, 2m), (line.CreditableAmount, line.ReturnableQuantity));
        Assert.Throws<InvalidOperationException>(() => line.AddCredit(15.01m, 0m));
        Assert.Throws<InvalidOperationException>(() => line.AddCredit(0m, 3m));
    }

    [Fact]
    public void FR_SAL_05_credit_note_with_returns_waits_for_the_warehouse_one_without_does_not()
    {
        var invoiceLine = Guid.NewGuid();
        var returns = CreditNote.CreateDraft(
            Company, Guid.NewGuid(), Guid.NewGuid(), Customer, Date, Date, Guid.NewGuid(), null,
            [new CreditNoteLineInput(invoiceLine, Article, CreditKind.Return, 2m, 10m, 20m, "PDV17", null, [], 7m)], Now);
        var reduction = CreditNote.CreateDraft(
            Company, Guid.NewGuid(), Guid.NewGuid(), Customer, Date, Date, Guid.NewGuid(), null,
            [new CreditNoteLineInput(invoiceLine, Article, CreditKind.ValueReduction, 0m, 0m, 5m, "PDV17", null, [], 0m)], Now);

        Assert.Throws<InvalidOperationException>(() => returns.Confirm("KNO-1", Now));
        Assert.Throws<InvalidOperationException>(() => reduction.BeginConfirmation());
        Assert.Null(reduction.WarehouseId);
        Assert.Throws<ArgumentException>(() => CreditNote.CreateDraft(
            Company, Guid.NewGuid(), Guid.NewGuid(), Customer, Date, Date, null, null,
            [new CreditNoteLineInput(invoiceLine, Article, CreditKind.Return, 1m, 10m, 10m, "PDV17", null, [], 7m)], Now));
    }

    [Fact]
    public void FR_SAL_02_order_from_offer_copies_customer_and_lines()
    {
        var offer = Offer.CreateDraft(Company, Customer, Date, null, null,
            [new SalesLineInput(Article, false, 2m, 5m, "PDV17"), new SalesLineInput(Guid.NewGuid(), true, 1m, 9m, "OSL")], Now);
        Assert.Throws<InvalidOperationException>(() => SalesOrder.CreateFromOffer(offer, Date, null, Now));
        offer.Send("PON-7", Now);

        var order = SalesOrder.CreateFromOffer(offer, Date, null, Now);

        Assert.Equal((offer.Id, Customer, DocumentStatus.Draft, "Ponuda PON-7"), (order.OfferId!.Value, order.CustomerId, order.Status, order.Note));
        Assert.Equal(offer.Lines.Select(l => (l.ArticleId, l.IsService, l.Quantity, l.UnitPrice, l.VatCode)), order.Lines.Select(l => (l.ArticleId, l.IsService, l.Quantity, l.UnitPrice, l.VatCode)));
    }
}
