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

/// <summary>Newest first; <see cref="Search"/> matches our number, the supplier's number or the supplier's name.</summary>
public sealed record ListSupplierInvoices(CompanyId CompanyId, Guid? PurchaseOrderId, DocumentStatus? Status, string? Search, int Page, int PageSize);

internal sealed class ListSupplierInvoicesValidator : AbstractValidator<ListSupplierInvoices>
{
    public ListSupplierInvoicesValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListSupplierInvoicesHandler(IPurchasingDbContext db, IValidator<ListSupplierInvoices> validator)
{
    public async Task<Result<PagedResponse<SupplierInvoiceSummaryResponse>>> HandleAsync(ListSupplierInvoices query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var invoices =
            from invoice in db.SupplierInvoices.AsNoTracking()
            join supplier in db.Suppliers.AsNoTracking() on invoice.SupplierId equals supplier.Id
            where invoice.CompanyId == query.CompanyId
            select new { invoice, SupplierName = supplier.Name };
        if (query.PurchaseOrderId is { } orderId)
        {
            invoices = invoices.Where(i => i.invoice.PurchaseOrderId == orderId);
        }

        if (query.Status is { } status)
        {
            invoices = invoices.Where(i => i.invoice.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            invoices = invoices.Where(i =>
                (i.invoice.Number != null && i.invoice.Number.ToLower().Contains(search))
                || i.invoice.SupplierInvoiceNumber.ToLower().Contains(search)
                || i.SupplierName.ToLower().Contains(search));
        }

        var totalCount = await invoices.CountAsync(cancellationToken);
        var page = await invoices
            .OrderByDescending(i => i.invoice.InvoiceDate)
            .ThenByDescending(i => i.invoice.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var items = page
            .Select(i => new SupplierInvoiceSummaryResponse(
                i.invoice.Id, i.invoice.Status.ToState(), i.invoice.Number, i.invoice.SupplierInvoiceNumber, i.invoice.PurchaseOrderId, i.SupplierName,
                i.invoice.InvoiceDate, i.invoice.DueDate, i.invoice.ReversesInvoiceId, i.invoice.ReversedByInvoiceId, i.invoice.GrossAmount))
            .ToList();
        return new PagedResponse<SupplierInvoiceSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
