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

/// <summary>Confirms a draft order: numbers it and makes it immutable (FR-PUR-01, NFR-INT-01).</summary>
public sealed record ConfirmPurchaseOrder(CompanyId CompanyId, Guid OrderId);

public sealed class ConfirmPurchaseOrderHandler(IPurchasingDbContext db, IPurchasingLocks locks, IClock clock)
{
    public async Task<Result> HandleAsync(ConfirmPurchaseOrder command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockOrderAsync(command.CompanyId, command.OrderId, cancellationToken))
        {
            return PurchasingErrors.OrderNotFound;
        }

        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.Id == command.OrderId, cancellationToken);
        if (!order.IsDraft)
        {
            return PurchasingErrors.OrderNotDraft;
        }

        var result = await OrderConfirmation.ConfirmAsync(db, locks, clock, order, cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
