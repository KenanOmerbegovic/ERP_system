using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Orders;

/// <summary>
/// Reverses a confirmed order nothing was delivered or invoiced on (FR-SAL-05): a storno order, confirmed and numbered
/// at once. Returns the storno's id. The reversed order takes no more deliveries or invoices.
/// </summary>
public sealed record StornoSalesOrder(CompanyId CompanyId, Guid OrderId);

public sealed class StornoSalesOrderHandler(ISalesDbContext db, ISalesLocks locks, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(StornoSalesOrder command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockOrderAsync(command.CompanyId, command.OrderId, cancellationToken))
        {
            return SalesErrors.OrderNotFound;
        }

        var order = await db.SalesOrders.Include(o => o.Lines).FirstAsync(o => o.Id == command.OrderId, cancellationToken);
        if (order.IsDraft)
        {
            return SalesErrors.OrderNotConfirmed;
        }

        if (order.IsStorno)
        {
            return SalesErrors.IsStorno;
        }

        if (order.ReversedByOrderId is not null)
        {
            return SalesErrors.AlreadyReversed;
        }

        // Delivery notes being confirmed count as delivered: their stock may already have left the warehouse.
        if (!order.CanBeReversed
            || await db.DeliveryNotes.AnyAsync(d => d.SalesOrderId == order.Id && d.Status == DocumentStatus.Confirming, cancellationToken))
        {
            return SalesErrors.OrderHasDeliveries;
        }

        var storno = order.CreateStorno(clock.Today, clock.UtcNow);
        db.SalesOrders.Add(storno);
        var result = await OrderConfirmation.ConfirmAsync(db, locks, clock, storno, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await transaction.CommitAsync(cancellationToken);
        return storno.Id;
    }
}
