using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Invoices;
using Erp.Modules.Sales.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Invoices;

/// <summary>
/// Newest first. <see cref="Search"/> matches the invoice's or the order's number, or the customer's name;
/// <see cref="OpenOnly"/> keeps confirmed invoices that credit notes can still be made on.
/// </summary>
public sealed record ListSalesInvoices(
    CompanyId CompanyId, Guid? SalesOrderId, Guid? CustomerId, DocumentStatus? Status, bool OpenOnly, string? Search, int Page, int PageSize);

internal sealed class ListSalesInvoicesValidator : AbstractValidator<ListSalesInvoices>
{
    public ListSalesInvoicesValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListSalesInvoicesHandler(ISalesDbContext db, IValidator<ListSalesInvoices> validator)
{
    public async Task<Result<PagedResponse<SalesInvoiceSummaryResponse>>> HandleAsync(ListSalesInvoices query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var invoices =
            from invoice in db.SalesInvoices.AsNoTracking()
            join order in db.SalesOrders.AsNoTracking() on invoice.SalesOrderId equals order.Id
            join customer in db.Customers.AsNoTracking() on invoice.CustomerId equals customer.Id
            where invoice.CompanyId == query.CompanyId
            select new { invoice, OrderNumber = order.Number, CustomerName = customer.Name };
        if (query.SalesOrderId is { } orderId)
        {
            invoices = invoices.Where(i => i.invoice.SalesOrderId == orderId);
        }

        if (query.CustomerId is { } customerId)
        {
            invoices = invoices.Where(i => i.invoice.CustomerId == customerId);
        }

        if (query.Status is { } status)
        {
            invoices = invoices.Where(i => i.invoice.Status == status);
        }

        if (query.OpenOnly)
        {
            invoices = invoices.Where(i =>
                i.invoice.Status == DocumentStatus.Confirmed && i.invoice.ReversesInvoiceId == null && i.invoice.ReversedByInvoiceId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            invoices = invoices.Where(i =>
                (i.invoice.Number != null && i.invoice.Number.ToLower().Contains(search))
                || (i.OrderNumber != null && i.OrderNumber.ToLower().Contains(search))
                || i.CustomerName.ToLower().Contains(search));
        }

        var totalCount = await invoices.CountAsync(cancellationToken);
        var items = await invoices
            .OrderByDescending(i => i.invoice.InvoiceDate)
            .ThenByDescending(i => i.invoice.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => new SalesInvoiceSummaryResponse(
                i.invoice.Id, i.invoice.Status == DocumentStatus.Draft ? Contracts.SalesDocumentState.Draft : Contracts.SalesDocumentState.Confirmed,
                i.invoice.Number, i.invoice.SalesOrderId, i.OrderNumber, i.CustomerName, i.invoice.InvoiceDate, i.invoice.DueDate,
                i.invoice.ReversesInvoiceId, i.invoice.ReversedByInvoiceId, i.invoice.GrossAmount))
            .ToListAsync(cancellationToken);
        return new PagedResponse<SalesInvoiceSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
