using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Offers;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Offers;

public sealed record GetOffer(CompanyId CompanyId, Guid OfferId);

public sealed class GetOfferHandler(ISalesDbContext db)
{
    public async Task<Result<OfferResponse>> HandleAsync(GetOffer query, CancellationToken cancellationToken)
    {
        var offer = await db.Offers.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.CompanyId == query.CompanyId && o.Id == query.OfferId, cancellationToken);
        if (offer is null)
        {
            return SalesErrors.OfferNotFound;
        }

        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == offer.CustomerId, cancellationToken);
        var articleIds = offer.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return new OfferResponse(
            offer.Id, offer.Status.ToState(), offer.Number, offer.CustomerId, customer.Code, customer.Name, offer.OfferDate, offer.ValidUntil,
            offer.Note, offer.SentAt, offer.DecidedAt, offer.SalesOrderId, offer.NetAmount,
            offer.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new OfferLineResponse(
                    l.Id, l.LineNumber, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name, l.IsService.ToArticleKind(),
                    l.Quantity, l.UnitPrice, l.VatCode, l.NetAmount))
                .ToList());
    }
}
