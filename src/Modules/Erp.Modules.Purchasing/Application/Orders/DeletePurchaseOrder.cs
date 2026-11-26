using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Orders;

/// <summary>Deletes a draft order. A confirmed order is reversed with a storno instead.</summary>
public sealed record DeletePurchaseOrder(CompanyId CompanyId, Guid OrderId);

public sealed class DeletePurchaseOrderHandler(IPurchasingDbContext db, IPurchasingLocks locks)
{
    public async Task<Result> HandleAsync(DeletePurchaseOrder command, CancellationToken cancellationToken)
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

        db.PurchaseOrders.Remove(order);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
