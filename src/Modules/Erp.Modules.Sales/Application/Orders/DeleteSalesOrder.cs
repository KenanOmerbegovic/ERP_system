using Erp.Modules.Sales.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Orders;

/// <summary>
/// Deletes a draft order. A confirmed order is reversed with a storno instead. Deleting the draft order an accepted
/// offer became undoes the acceptance: the offer is sent (open) again.
/// </summary>
public sealed record DeleteSalesOrder(CompanyId CompanyId, Guid OrderId);

public sealed class DeleteSalesOrderHandler(ISalesDbContext db, ISalesLocks locks)
{
    public async Task<Result> HandleAsync(DeleteSalesOrder command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockOrderAsync(command.CompanyId, command.OrderId, cancellationToken))
        {
            return SalesErrors.OrderNotFound;
        }

        var order = await db.SalesOrders.Include(o => o.Lines).FirstAsync(o => o.Id == command.OrderId, cancellationToken);
        if (!order.IsDraft)
        {
            return SalesErrors.OrderNotDraft;
        }

        if (order.OfferId is { } offerId && await locks.LockOfferAsync(command.CompanyId, offerId, cancellationToken))
        {
            var offer = await db.Offers.FirstAsync(o => o.Id == offerId, cancellationToken);
            offer.CancelAcceptance(order.Id);
        }

        db.SalesOrders.Remove(order);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
