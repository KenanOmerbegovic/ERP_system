using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Purchasing.Domain.Invoices;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.Modules.Purchasing.Domain.Receipts;

namespace Erp.Modules.Purchasing.Application.Common;

/// <summary>The integration events confirmed documents publish; a storno's quantities and amounts are negative.</summary>
internal static class PurchasingEvents
{
    public static PurchaseOrderConfirmed OrderConfirmed(PurchaseOrder order, DateTimeOffset now)
    {
        var sign = order.IsStorno ? -1m : 1m;
        return new PurchaseOrderConfirmed(
            Guid.CreateVersion7(), now, order.CompanyId.Value, order.Id, order.Number!, order.SupplierId, order.OrderDate, order.ReversesOrderId,
            sign * order.NetAmount,
            order.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new PurchaseOrderEventLine(l.ArticleId, sign * l.Quantity, l.UnitPrice, l.VatCode, sign * l.NetAmount))
                .ToList());
    }

    public static GoodsReceived Received(GoodsReceipt receipt, DateTimeOffset now)
    {
        var sign = receipt.IsStorno ? -1m : 1m;
        return new GoodsReceived(
            Guid.CreateVersion7(), now, receipt.CompanyId.Value, receipt.Id, receipt.Number!, receipt.PurchaseOrderId, receipt.SupplierId,
            receipt.WarehouseId, receipt.ReceiptDate, receipt.StockDocumentId!.Value, receipt.ReversesReceiptId, sign * (receipt.TotalValue ?? 0m),
            receipt.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new GoodsReceivedLine(l.ArticleId, l.OrderLineId, sign * l.Quantity, l.UnitCost, sign * (l.Value ?? 0m)))
                .ToList());
    }

    public static SupplierInvoiceRegistered InvoiceRegistered(SupplierInvoice invoice, DateTimeOffset now)
    {
        var sign = invoice.IsStorno ? -1m : 1m;
        return new SupplierInvoiceRegistered(
            Guid.CreateVersion7(), now, invoice.CompanyId.Value, invoice.Id, invoice.Number!, invoice.SupplierInvoiceNumber, invoice.SupplierId,
            invoice.PurchaseOrderId, invoice.InvoiceDate, invoice.DueDate, invoice.ReversesInvoiceId,
            sign * invoice.NetAmount, sign * invoice.VatAmount, sign * invoice.GrossAmount,
            invoice.VatTotals.Select(t => new SupplierInvoiceEventVatTotal(t.VatCode, t.Rate, sign * t.Base, sign * t.VatAmount)).ToList(),
            invoice.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new SupplierInvoiceEventLine(
                    l.ArticleId, l.ReceiptId, l.ReceiptLineId, sign * l.Quantity, l.UnitPrice, l.VatCode,
                    sign * l.NetAmount, sign * l.VatAmount, sign * l.ReceivedValue, sign * l.PriceDifference))
                .ToList());
    }
}
