using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Numbering;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Orders;

/// <summary>
/// Reverses a confirmed order nothing was received or invoiced on: a storno order, confirmed and numbered at once.
/// Returns the storno's id. The reversed order takes no more receipts.
/// </summary>
public sealed record StornoPurchaseOrder(CompanyId CompanyId, Guid OrderId);

public sealed class StornoPurchaseOrderHandler(IPurchasingDbContext db, IPurchasingLocks locks, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(StornoPurchaseOrder command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockOrderAsync(command.CompanyId, command.OrderId, cancellationToken))
        {
            return PurchasingErrors.OrderNotFound;
        }

        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.Id == command.OrderId, cancellationToken);
        if (order.IsDraft)
        {
            return PurchasingErrors.OrderNotConfirmed;
        }

        if (order.IsStorno)
        {
            return PurchasingErrors.IsStorno;
        }

        if (order.ReversedByOrderId is not null)
        {
            return PurchasingErrors.AlreadyReversed;
        }

        // Receipts being confirmed count as received: their stock may already be in the warehouse.
        if (!order.CanBeReversed || await db.GoodsReceipts.AnyAsync(r => r.PurchaseOrderId == order.Id && r.Status == Domain.DocumentStatus.Confirming, cancellationToken))
        {
            return PurchasingErrors.OrderHasReceipts;
        }

        var storno = order.CreateStorno(clock.Today, clock.UtcNow);
        db.PurchaseOrders.Add(storno);
        var result = await OrderConfirmation.ConfirmAsync(db, locks, clock, storno, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await transaction.CommitAsync(cancellationToken);
        return storno.Id;
    }
}
