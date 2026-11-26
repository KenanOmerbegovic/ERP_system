using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Contracts.Invoices;
using Erp.Modules.Purchasing.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Invoices;

/// <summary>The order's confirmed, not reversed receipt lines that still have quantity to invoice.</summary>
public sealed record ListInvoiceableReceiptLines(CompanyId CompanyId, Guid PurchaseOrderId);

public sealed class ListInvoiceableReceiptLinesHandler(IPurchasingDbContext db)
{
    public async Task<Result<IReadOnlyList<InvoiceableReceiptLineResponse>>> HandleAsync(ListInvoiceableReceiptLines query, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.CompanyId == query.CompanyId && o.Id == query.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            return PurchasingErrors.OrderNotFound;
        }

        var receipts = await db.GoodsReceipts.AsNoTracking().Include(r => r.Lines)
            .Where(r => r.PurchaseOrderId == order.Id && r.Status == DocumentStatus.Confirmed && r.ReversesReceiptId == null && r.ReversedByReceiptId == null)
            .OrderBy(r => r.ReceiptDate)
            .ThenBy(r => r.Number)
            .ToListAsync(cancellationToken);
        var articleIds = order.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return Result.Success<IReadOnlyList<InvoiceableReceiptLineResponse>>(receipts
            .SelectMany(r => r.Lines.OrderBy(l => l.LineNumber).Where(l => l.UninvoicedQuantity > 0m).Select(l => new InvoiceableReceiptLineResponse(
                r.Id, r.Number!, r.ReceiptDate, l.Id, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name,
                l.Quantity, l.InvoicedQuantity, l.UnitCost, order.Line(l.OrderLineId).VatCode)))
            .ToList());
    }
}
