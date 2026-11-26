using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Contracts.Invoices;
using Erp.Modules.Sales.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Invoices;

/// <summary>
/// What an invoice of the order can still take: lines of confirmed, not reversed delivery notes with quantity left to
/// invoice (goods), then service order lines with quantity left to invoice.
/// </summary>
public sealed record ListInvoiceableLines(CompanyId CompanyId, Guid SalesOrderId);

public sealed class ListInvoiceableLinesHandler(ISalesDbContext db)
{
    public async Task<Result<IReadOnlyList<InvoiceableLineResponse>>> HandleAsync(ListInvoiceableLines query, CancellationToken cancellationToken)
    {
        var order = await db.SalesOrders.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.CompanyId == query.CompanyId && o.Id == query.SalesOrderId, cancellationToken);
        if (order is null)
        {
            return SalesErrors.OrderNotFound;
        }

        var notes = await db.DeliveryNotes.AsNoTracking().Include(d => d.Lines)
            .Where(d => d.SalesOrderId == order.Id && d.Status == DocumentStatus.Confirmed && d.ReversesDeliveryNoteId == null && d.ReversedByDeliveryNoteId == null)
            .OrderBy(d => d.DeliveryDate)
            .ThenBy(d => d.Number)
            .ToListAsync(cancellationToken);
        var articleIds = order.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var goods = notes.SelectMany(d => d.Lines.OrderBy(l => l.LineNumber).Where(l => l.UninvoicedQuantity > 0m).Select(l =>
        {
            var orderLine = order.Line(l.OrderLineId);
            return new InvoiceableLineResponse(
                orderLine.Id, d.Id, d.Number, d.DeliveryDate, l.Id, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name,
                ArticleKind.Goods, l.Quantity, l.InvoicedQuantity, orderLine.UnitPrice, orderLine.VatCode);
        }));
        var services = order.Lines.OrderBy(l => l.LineNumber).Where(l => l.IsService && l.InvoiceableQuantity > 0m).Select(l =>
            new InvoiceableLineResponse(
                l.Id, null, null, null, null, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name,
                ArticleKind.Service, l.Quantity, l.InvoicedQuantity, l.UnitPrice, l.VatCode));
        return Result.Success<IReadOnlyList<InvoiceableLineResponse>>(goods.Concat(services).ToList());
    }
}
