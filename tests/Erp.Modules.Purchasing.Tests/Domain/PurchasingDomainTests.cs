using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Invoices;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Purchasing.Tests.Domain;

/// <summary>Example-based tests of the purchasing documents, without a database.</summary>
public class PurchasingDomainTests
{
    private static readonly CompanyId Company = CompanyId.New();
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 2);

    [Fact]
    public void FR_PUR_01_line_amounts_are_rounded_to_fening()
    {
        var order = Order(new PurchaseOrderLineInput(Guid.NewGuid(), 3m, 3.3333m, "PDV17"));

        Assert.Equal(10.00m, order.NetAmount); // 9.9999 → 10.00
    }

    [Fact]
    public void FR_PUR_01_received_quantity_never_exceeds_the_order()
    {
        var order = Order(new PurchaseOrderLineInput(Guid.NewGuid(), 5m, 1m, "PDV17"));
        var line = order.Lines[0];

        order.RecordReceived(line.Id, 5m);

        Assert.Equal(0m, line.OpenQuantity);
        Assert.Throws<InvalidOperationException>(() => order.RecordReceived(line.Id, 0.0001m));
        Assert.Throws<InvalidOperationException>(() => order.RecordInvoiced(line.Id, 6m));
    }

    [Fact]
    public void NFR_INT_01_order_with_receipts_cannot_be_reversed_and_confirmed_order_cannot_change()
    {
        var order = Order(new PurchaseOrderLineInput(Guid.NewGuid(), 5m, 1m, "PDV17"));
        order.RecordReceived(order.Lines[0].Id, 1m);

        Assert.False(order.CanBeReversed);
        Assert.Throws<InvalidOperationException>(() => order.CreateStorno(Today, Now));
        Assert.Throws<InvalidOperationException>(() => order.UpdateDraft(Guid.NewGuid(), Today, null, null, null, [new PurchaseOrderLineInput(Guid.NewGuid(), 1m, 1m, "PDV17")]));
    }

    [Fact]
    public void FR_PUR_01_receipt_takes_its_unit_cost_from_the_order_and_goes_through_confirming()
    {
        var order = Order(new PurchaseOrderLineInput(Guid.NewGuid(), 5m, 2.5m, "PDV17"));
        var receipt = GoodsReceipt.CreateDraft(Company, order, Guid.NewGuid(), Today, null, [new GoodsReceiptLineInput(order.Lines[0].Id, 2m, null, null, [])], Now);

        receipt.BeginConfirmation();
        Assert.Throws<InvalidOperationException>(() => receipt.UpdateDraft(order, receipt.WarehouseId, Today, null, []));
        receipt.CompleteConfirmation("PRI-2026-00001", Guid.NewGuid(), "PRM-2026-00001", [5.00m], Now);

        Assert.Equal((2.5m, 5.00m, DocumentStatus.Confirmed), (receipt.Lines[0].UnitCost, receipt.TotalValue, receipt.Status));
    }

    [Fact]
    public void FR_PUR_02_price_difference_is_invoiced_net_minus_received_value()
    {
        var invoice = SupplierInvoice.CreateDraft(Company, Guid.NewGuid(), Guid.NewGuid(), "F-1", Today, Today.AddDays(15), null,
            [new SupplierInvoiceLineInput(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReceivedUnitCost: 1.10m, Quantity: 3m, UnitPrice: 1.05m, "PDV17")], Now);

        var line = invoice.Lines[0];

        Assert.Equal((3.15m, 3.30m, -0.15m), (line.NetAmount, line.ReceivedValue, line.PriceDifference));
    }

    [Fact]
    public void FR_PUR_02_invoice_due_date_cannot_precede_invoice_date()
    {
        Assert.Throws<ArgumentException>(() => SupplierInvoice.CreateDraft(Company, Guid.NewGuid(), Guid.NewGuid(), "F-1", Today, Today.AddDays(-1), null,
            [new SupplierInvoiceLineInput(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, 1m, 1m, "PDV17")], Now));
    }

    private static PurchaseOrder Order(params PurchaseOrderLineInput[] lines)
    {
        var order = PurchaseOrder.CreateDraft(Company, Guid.NewGuid(), Today, null, null, null, lines, Now);
        order.Confirm("NAR-2026-00001", Now);
        return order;
    }
}
