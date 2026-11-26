using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Offers;
using Erp.Modules.Sales.Domain.Offers;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Offers;

/// <summary>Newest first. <see cref="Search"/> matches the number or the customer's name.</summary>
public sealed record ListOffers(CompanyId CompanyId, Guid? CustomerId, OfferStatus? Status, string? Search, int Page, int PageSize);

internal sealed class ListOffersValidator : AbstractValidator<ListOffers>
{
    public ListOffersValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListOffersHandler(ISalesDbContext db, IValidator<ListOffers> validator)
{
    public async Task<Result<PagedResponse<OfferSummaryResponse>>> HandleAsync(ListOffers query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var offers =
            from offer in db.Offers.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on offer.CustomerId equals customer.Id
            where offer.CompanyId == query.CompanyId
            select new { offer, CustomerName = customer.Name };
        if (query.CustomerId is { } customerId)
        {
            offers = offers.Where(o => o.offer.CustomerId == customerId);
        }

        if (query.Status is { } status)
        {
            offers = offers.Where(o => o.offer.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            offers = offers.Where(o => (o.offer.Number != null && o.offer.Number.ToLower().Contains(search)) || o.CustomerName.ToLower().Contains(search));
        }

        var totalCount = await offers.CountAsync(cancellationToken);
        var page = await offers
            .OrderByDescending(o => o.offer.OfferDate)
            .ThenByDescending(o => o.offer.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new { o.offer, o.CustomerName, o.offer.Lines })
            .ToListAsync(cancellationToken);
        var items = page
            .Select(o => new OfferSummaryResponse(
                o.offer.Id, o.offer.Status.ToState(), o.offer.Number, o.offer.CustomerId, o.CustomerName, o.offer.OfferDate, o.offer.ValidUntil,
                o.offer.SalesOrderId, o.Lines.Sum(l => l.NetAmount)))
            .ToList();
        return new PagedResponse<OfferSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
