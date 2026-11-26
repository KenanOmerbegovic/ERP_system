using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Domain.Offers;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Offers;

/// <summary>The customer declined a sent offer; it stays on record as rejected.</summary>
public sealed record RejectOffer(CompanyId CompanyId, Guid OfferId);

public sealed class RejectOfferHandler(ISalesDbContext db, ISalesLocks locks, IClock clock)
{
    public async Task<Result> HandleAsync(RejectOffer command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockOfferAsync(command.CompanyId, command.OfferId, cancellationToken))
        {
            return SalesErrors.OfferNotFound;
        }

        var offer = await db.Offers.FirstAsync(o => o.Id == command.OfferId, cancellationToken);
        if (offer.Status != OfferStatus.Sent)
        {
            return SalesErrors.OfferNotSent;
        }

        offer.Reject(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
