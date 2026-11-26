using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Numbering;
using Erp.Modules.Purchasing.Application.Receipts;
using Erp.Modules.Purchasing.Domain.Invoices;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Invoices;

internal static class InvoicedQuantities
{
    /// <summary>Adds (or, for a storno, subtracts) the invoice's quantities on its receipt lines and order lines.</summary>
    public static async Task ApplyAsync(IPurchasingDbContext db, PurchaseOrder order, SupplierInvoice invoice, CancellationToken cancellationToken)
    {
        var sign = invoice.IsStorno ? -1m : 1m;
        var receiptIds = invoice.Lines.Select(l => l.ReceiptId).Distinct().ToList();
        var receipts = await db.GoodsReceipts.Include(r => r.Lines).Where(r => receiptIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        foreach (var line in invoice.Lines)
        {
            receipts[line.ReceiptId].Line(line.ReceiptLineId).AddInvoiced(sign * line.Quantity);
            order.RecordInvoiced(line.OrderLineId, sign * line.Quantity);
        }
    }
}
