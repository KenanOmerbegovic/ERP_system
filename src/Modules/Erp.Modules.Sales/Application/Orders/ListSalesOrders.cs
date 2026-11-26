using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Orders;
using Erp.Modules.Sales.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Orders;

/// <summary>
/// Newest first. <see cref="Search"/> matches the number or the customer's name; <see cref="OpenOnly"/> keeps orders that
/// take deliveries and invoices.
/// </summary>
public sealed record ListSalesOrders(
    CompanyId CompanyId, Guid? CustomerId, DocumentStatus? Status, bool OpenOnly, string? Search, int Page, int PageSize);

internal sealed class ListSalesOrdersValidator : AbstractValidator<ListSalesOrders>
{
    public ListSalesOrdersValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListSalesOrdersHandler(ISalesDbContext db, IValidator<ListSalesOrders> validator)
{
    public async Task<Result<PagedResponse<SalesOrderSummaryResponse>>> HandleAsync(ListSalesOrders query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var orders =
            from order in db.SalesOrders.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on order.CustomerId equals customer.Id
            where order.CompanyId == query.CompanyId
            select new { order, CustomerName = customer.Name };
        if (query.CustomerId is { } customerId)
        {
            orders = orders.Where(o => o.order.CustomerId == customerId);
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
            orders = orders.Where(o => (o.order.Number != null && o.order.Number.ToLower().Contains(search)) || o.CustomerName.ToLower().Contains(search));
        }

        var totalCount = await orders.CountAsync(cancellationToken);
        var page = await orders
            .OrderByDescending(o => o.order.OrderDate)
            .ThenByDescending(o => o.order.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new { o.order, o.CustomerName, o.order.Lines })
            .ToListAsync(cancellationToken);
        var items = page
            .Select(o => new SalesOrderSummaryResponse(
                o.order.Id, o.order.Status.ToState(), o.order.Number, o.order.CustomerId, o.CustomerName, o.order.OrderDate,
                o.order.ReversesOrderId, o.order.ReversedByOrderId, o.Lines.Sum(l => l.NetAmount),
                o.Lines.All(l => l.OpenQuantity == 0m), o.Lines.All(l => l.InvoicedQuantity == l.Quantity)))
            .ToList();
        return new PagedResponse<SalesOrderSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
