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

/// <summary>Newest first. <see cref="Search"/> matches the number or the supplier's name; <see cref="OpenOnly"/> keeps orders that take receipts.</summary>
public sealed record ListPurchaseOrders(
    CompanyId CompanyId, Guid? SupplierId, DocumentStatus? Status, bool OpenOnly, string? Search, int Page, int PageSize);

internal sealed class ListPurchaseOrdersValidator : AbstractValidator<ListPurchaseOrders>
{
    public ListPurchaseOrdersValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListPurchaseOrdersHandler(IPurchasingDbContext db, IValidator<ListPurchaseOrders> validator)
{
    public async Task<Result<PagedResponse<PurchaseOrderSummaryResponse>>> HandleAsync(ListPurchaseOrders query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var orders =
            from order in db.PurchaseOrders.AsNoTracking()
            join supplier in db.Suppliers.AsNoTracking() on order.SupplierId equals supplier.Id
            where order.CompanyId == query.CompanyId
            select new { order, SupplierName = supplier.Name };
        if (query.SupplierId is { } supplierId)
        {
            orders = orders.Where(o => o.order.SupplierId == supplierId);
        }

        if (query.Status is { } status)
        {
            orders = orders.Where(o => o.order.Status == status);
        }

        if (query.OpenOnly)
        {
            orders = orders.Where(o => o.order.Status == DocumentStatus.Confirmed && o.order.ReversesOrderId == null && o.order.ReversedByOrderId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            orders = orders.Where(o => (o.order.Number != null && o.order.Number.ToLower().Contains(search)) || o.SupplierName.ToLower().Contains(search));
        }

        var totalCount = await orders.CountAsync(cancellationToken);
        var page = await orders
            .OrderByDescending(o => o.order.OrderDate)
            .ThenByDescending(o => o.order.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new { o.order, o.SupplierName, o.order.Lines })
            .ToListAsync(cancellationToken);
        var items = page
            .Select(o => new PurchaseOrderSummaryResponse(
                o.order.Id, o.order.Status.ToState(), o.order.Number, o.order.SupplierId, o.SupplierName, o.order.OrderDate,
                o.order.ReversesOrderId, o.order.ReversedByOrderId,
                o.Lines.Sum(l => l.NetAmount), o.Lines.All(l => l.OpenQuantity == 0m)))
            .ToList();
        return new PagedResponse<PurchaseOrderSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
