using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Contracts.Orders;
using Erp.Modules.Purchasing.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Orders;

public sealed record GetPurchaseOrder(CompanyId CompanyId, Guid OrderId);

public sealed class GetPurchaseOrderHandler(IPurchasingDbContext db)
{
    public async Task<Result<PurchaseOrderResponse>> HandleAsync(GetPurchaseOrder query, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.CompanyId == query.CompanyId && o.Id == query.OrderId, cancellationToken);
        if (order is null)
        {
            return PurchasingErrors.OrderNotFound;
        }

        var supplier = await db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == order.SupplierId, cancellationToken);
        var articleIds = order.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return new PurchaseOrderResponse(
            order.Id, order.Status.ToState(), order.Number, order.SupplierId, supplier.Code, supplier.Name, order.OrderDate, order.ExpectedDate,
            order.WarehouseId, order.Note, order.ReversesOrderId, order.ReversedByOrderId, order.ConfirmedAt, order.NetAmount,
            order.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new PurchaseOrderLineResponse(
                    l.Id, l.LineNumber, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name, articles[l.ArticleId].Tracking.ToContract(),
                    l.Quantity, l.UnitPrice, l.VatCode, l.NetAmount, l.ReceivedQuantity, l.InvoicedQuantity, l.OpenQuantity))
                .ToList());
    }
}
