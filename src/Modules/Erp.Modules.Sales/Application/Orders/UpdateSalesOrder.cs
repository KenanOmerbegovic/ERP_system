using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Orders;

/// <summary>Replaces a draft order's content. A confirmed order cannot be changed (FR-SAL-05, NFR-INT-01).</summary>
public sealed record UpdateSalesOrder(
    CompanyId CompanyId,
    Guid OrderId,
    Guid CustomerId,
    DateOnly OrderDate,
    Guid? WarehouseId,
    string? Note,
    IReadOnlyList<SalesDraftLine> Lines) : ISalesOrderDraft;

internal sealed class UpdateSalesOrderValidator : AbstractValidator<UpdateSalesOrder>
{
    public UpdateSalesOrderValidator(IClock clock)
    {
        Include(new SalesOrderDraftValidator(clock));
    }
}

public sealed class UpdateSalesOrderHandler(ISalesDbContext db, ISalesLocks locks, IVatRateLookup vatRates, IValidator<UpdateSalesOrder> validator)
{
    public async Task<Result> HandleAsync(UpdateSalesOrder command, CancellationToken cancellationToken)
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

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var lines = await SalesLineResolution.ResolveAsync(db, vatRates, command.CompanyId, command.CustomerId, command.OrderDate, command.Lines, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        order.UpdateDraft(command.CustomerId, command.OrderDate, command.WarehouseId, command.Note, lines.Value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
