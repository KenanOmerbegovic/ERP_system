using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Offers;

/// <summary>
/// Sends a draft offer (FR-SAL-01): re-checks it, numbers it and freezes it. Printing or e-mailing it is up to the user.
/// </summary>
public sealed record SendOffer(CompanyId CompanyId, Guid OfferId);

public sealed class SendOfferHandler(ISalesDbContext db, ISalesLocks locks, IVatRateLookup vatRates, IClock clock)
{
    public async Task<Result> HandleAsync(SendOffer command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockOfferAsync(command.CompanyId, command.OfferId, cancellationToken))
        {
            return SalesErrors.OfferNotFound;
        }

        var offer = await db.Offers.Include(o => o.Lines).FirstAsync(o => o.Id == command.OfferId, cancellationToken);
        if (!offer.IsDraft)
        {
            return SalesErrors.OfferNotDraft;
        }

        var draftLines = offer.Lines.OrderBy(l => l.LineNumber).Select(l => new SalesDraftLine(l.ArticleId, l.Quantity, l.UnitPrice, l.VatCode)).ToList();
        var lines = await SalesLineResolution.ResolveAsync(db, vatRates, offer.CompanyId, offer.CustomerId, offer.OfferDate, draftLines, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        var number = await DocumentNumbers.NextAsync(db, locks, command.CompanyId, SalesDocumentType.Offer, offer.OfferDate, cancellationToken);
        if (number.IsFailure)
        {
            return number.Error;
        }

        offer.Send(number.Value, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
