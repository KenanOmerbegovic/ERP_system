using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.Modules.Sales.Domain.Invoices;
using Erp.Modules.Sales.Domain.Orders;

namespace Erp.Modules.Sales.Application.Common;

/// <summary>The integration events confirmed documents publish; a storno's quantities and amounts are negative.</summary>
internal static class SalesEvents
{
    public static SalesOrderConfirmed OrderConfirmed(SalesOrder order, DateTimeOffset now)
    {
        var sign = order.IsStorno ? -1m : 1m;
        return new SalesOrderConfirmed(
            Guid.CreateVersion7(), now, order.CompanyId.Value, order.Id, order.Number!, order.CustomerId, order.OrderDate, order.OfferId,
            order.ReversesOrderId, sign * order.NetAmount,
            order.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new SalesOrderEventLine(l.ArticleId, l.IsService, sign * l.Quantity, l.UnitPrice, l.VatCode, sign * l.NetAmount))
                .ToList());
    }

    public static GoodsDelivered Delivered(DeliveryNote note, DateTimeOffset now)
    {
        var sign = note.IsStorno ? -1m : 1m;
        return new GoodsDelivered(
            Guid.CreateVersion7(), now, note.CompanyId.Value, note.Id, note.Number!, note.SalesOrderId, note.CustomerId, note.WarehouseId,
            note.DeliveryDate, note.StockDocumentId!.Value, note.ReversesDeliveryNoteId, sign * (note.CostValue ?? 0m),
            note.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new GoodsDeliveredLine(l.ArticleId, l.OrderLineId, l.Id, sign * l.Quantity, sign * (l.CostValue ?? 0m)))
                .ToList());
    }

    public static InvoiceIssued InvoiceIssued(SalesInvoice invoice, DateTimeOffset now)
    {
        var sign = invoice.IsStorno ? -1m : 1m;
        return new InvoiceIssued(
            Guid.CreateVersion7(), now, invoice.CompanyId.Value, invoice.Id, invoice.Number!, invoice.CustomerId, invoice.SalesOrderId,
            invoice.InvoiceDate, invoice.DueDate, invoice.ReversesInvoiceId,
            sign * invoice.NetAmount, sign * invoice.VatAmount, sign * invoice.GrossAmount,
            invoice.VatTotals.Select(t => new SalesVatTotal(t.VatCode, t.Rate, sign * t.Base, sign * t.VatAmount)).ToList(),
            invoice.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new InvoiceIssuedLine(
                    l.Id, l.ArticleId, l.IsService, l.OrderLineId, l.DeliveryNoteId, l.DeliveryLineId, sign * l.Quantity, l.UnitPrice, l.VatCode,
                    sign * l.NetAmount, sign * l.VatAmount))
                .ToList());
    }

    public static CreditNoteIssued CreditNoteIssued(CreditNote creditNote, DateTimeOffset now)
    {
        var sign = creditNote.IsStorno ? -1m : 1m;
        return new CreditNoteIssued(
            Guid.CreateVersion7(), now, creditNote.CompanyId.Value, creditNote.Id, creditNote.Number!, creditNote.InvoiceId, creditNote.CustomerId,
            creditNote.SalesOrderId, creditNote.CreditNoteDate, creditNote.WarehouseId, creditNote.StockDocumentId, creditNote.ReversesCreditNoteId,
            sign * creditNote.NetAmount, sign * creditNote.VatAmount, sign * creditNote.GrossAmount, sign * (creditNote.ReturnedCostValue ?? 0m),
            creditNote.VatTotals.Select(t => new SalesVatTotal(t.VatCode, t.Rate, sign * t.Base, sign * t.VatAmount)).ToList(),
            creditNote.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new CreditNoteIssuedLine(
                    l.ArticleId, l.InvoiceLineId, l.Kind.ToContract(), sign * l.Quantity, l.VatCode, sign * l.NetAmount, sign * l.VatAmount,
                    sign * (l.ReturnedCostValue ?? 0m)))
                .ToList());
    }
}
