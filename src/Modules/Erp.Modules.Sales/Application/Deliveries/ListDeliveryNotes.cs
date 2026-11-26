using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Deliveries;
using Erp.Modules.Sales.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Deliveries;

/// <summary>Newest first. <see cref="Search"/> matches the note's or the order's number, or the customer's name.</summary>
public sealed record ListDeliveryNotes(CompanyId CompanyId, Guid? SalesOrderId, DocumentStatus? Status, string? Search, int Page, int PageSize);

internal sealed class ListDeliveryNotesValidator : AbstractValidator<ListDeliveryNotes>
{
    public ListDeliveryNotesValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListDeliveryNotesHandler(ISalesDbContext db, IValidator<ListDeliveryNotes> validator)
{
    public async Task<Result<PagedResponse<DeliveryNoteSummaryResponse>>> HandleAsync(ListDeliveryNotes query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var notes =
            from note in db.DeliveryNotes.AsNoTracking()
            join order in db.SalesOrders.AsNoTracking() on note.SalesOrderId equals order.Id
            join customer in db.Customers.AsNoTracking() on note.CustomerId equals customer.Id
            where note.CompanyId == query.CompanyId
            select new { note, OrderNumber = order.Number, CustomerName = customer.Name };
        if (query.SalesOrderId is { } orderId)
        {
            notes = notes.Where(n => n.note.SalesOrderId == orderId);
        }

        if (query.Status is { } status)
        {
            notes = notes.Where(n => n.note.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            notes = notes.Where(n =>
                (n.note.Number != null && n.note.Number.ToLower().Contains(search))
                || (n.OrderNumber != null && n.OrderNumber.ToLower().Contains(search))
                || n.CustomerName.ToLower().Contains(search));
        }

        var totalCount = await notes.CountAsync(cancellationToken);
        var page = await notes
            .OrderByDescending(n => n.note.DeliveryDate)
            .ThenByDescending(n => n.note.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new { n.note, n.OrderNumber, n.CustomerName, n.note.Lines })
            .ToListAsync(cancellationToken);
        var items = page
            .Select(n => new DeliveryNoteSummaryResponse(
                n.note.Id, n.note.Status.ToState(), n.note.Number, n.note.SalesOrderId, n.OrderNumber, n.CustomerName, n.note.WarehouseId,
                n.note.DeliveryDate, n.note.ReversesDeliveryNoteId, n.note.ReversedByDeliveryNoteId,
                n.note.Status == DocumentStatus.Confirmed ? n.Lines.Sum(l => l.CostValue ?? 0m) : null))
            .ToList();
        return new PagedResponse<DeliveryNoteSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
