using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Orders;

public sealed record GetSalesOrder(CompanyId CompanyId, Guid OrderId);

public sealed class GetSalesOrderHandler(ISalesDbContext db)
{
    public async Task<Result<SalesOrderResponse>> HandleAsync(GetSalesOrder query, CancellationToken cancellationToken)
    {
        var order = await db.SalesOrders.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.CompanyId == query.CompanyId && o.Id == query.OrderId, cancellationToken);
        if (order is null)
        {
            return SalesErrors.OrderNotFound;
        }

        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == order.CustomerId, cancellationToken);
        var offerNumber = order.OfferId is { } offerId
            ? await db.Offers.AsNoTracking().Where(o => o.Id == offerId).Select(o => o.Number).FirstOrDefaultAsync(cancellationToken)
            : null;
        var articleIds = order.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return new SalesOrderResponse(
            order.Id, order.Status.ToState(), order.Number, order.CustomerId, customer.Code, customer.Name, order.OrderDate, order.WarehouseId,
            order.Note, order.OfferId, offerNumber, order.ReversesOrderId, order.ReversedByOrderId, order.ConfirmedAt, order.NetAmount,
            order.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new SalesOrderLineResponse(
                    l.Id, l.LineNumber, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name, l.IsService.ToArticleKind(),
                    articles[l.ArticleId].Tracking.ToContract(), l.Quantity, l.UnitPrice, l.VatCode, l.NetAmount, l.DeliveredQuantity,
                    l.InvoicedQuantity, l.OpenQuantity, l.InvoiceableQuantity))
                .ToList());
    }
}
