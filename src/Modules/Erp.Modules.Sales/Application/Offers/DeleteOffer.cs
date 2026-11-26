using Erp.Modules.Sales.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Offers;

/// <summary>Deletes a draft offer. A sent offer stays on record; it is accepted or rejected.</summary>
public sealed record DeleteOffer(CompanyId CompanyId, Guid OfferId);

public sealed class DeleteOfferHandler(ISalesDbContext db, ISalesLocks locks)
{
    public async Task<Result> HandleAsync(DeleteOffer command, CancellationToken cancellationToken)
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

        db.Offers.Remove(offer);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
