using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Sales.Application.Orders;

/// <summary>Saves a draft sales order (an order taken directly, without an offer).</summary>
public sealed record CreateSalesOrder(
    CompanyId CompanyId,
    Guid CustomerId,
    DateOnly OrderDate,
    Guid? WarehouseId,
    string? Note,
    IReadOnlyList<SalesDraftLine> Lines) : ISalesOrderDraft;

internal sealed class CreateSalesOrderValidator : AbstractValidator<CreateSalesOrder>
{
    public CreateSalesOrderValidator(IClock clock)
    {
        Include(new SalesOrderDraftValidator(clock));
    }
}

public sealed class CreateSalesOrderHandler(ISalesDbContext db, IVatRateLookup vatRates, IValidator<CreateSalesOrder> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateSalesOrder command, CancellationToken cancellationToken)
    {
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

        var order = SalesOrder.CreateDraft(command.CompanyId, command.CustomerId, command.OrderDate, command.WarehouseId, command.Note, lines.Value, clock.UtcNow);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return order.Id;
    }
}
