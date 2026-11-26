using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Orders;

/// <summary>Replaces a draft order's content. A confirmed order cannot be changed (NFR-INT-01).</summary>
public sealed record UpdatePurchaseOrder(
    CompanyId CompanyId,
    Guid OrderId,
    Guid SupplierId,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid? WarehouseId,
    string? Note,
    IReadOnlyList<PurchaseOrderDraftLine> Lines) : IPurchaseOrderDraft;

internal sealed class UpdatePurchaseOrderValidator : AbstractValidator<UpdatePurchaseOrder>
{
    public UpdatePurchaseOrderValidator(IClock clock)
    {
        Include(new PurchaseOrderDraftValidator(clock));
    }
}

public sealed class UpdatePurchaseOrderHandler(
    IPurchasingDbContext db, IPurchasingLocks locks, IVatRateLookup vatRates, IValidator<UpdatePurchaseOrder> validator)
{
    public async Task<Result> HandleAsync(UpdatePurchaseOrder command, CancellationToken cancellationToken)
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

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var lines = await PurchaseOrderDraftChecks.ResolveAsync(db, vatRates, command, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        order.UpdateDraft(command.SupplierId, command.OrderDate, command.ExpectedDate, command.WarehouseId, command.Note, lines.Value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
