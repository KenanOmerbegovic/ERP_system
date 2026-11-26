using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Purchasing.Application.Orders;

/// <summary>Saves a draft purchase order (FR-PUR-01).</summary>
public sealed record CreatePurchaseOrder(
    CompanyId CompanyId,
    Guid SupplierId,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid? WarehouseId,
    string? Note,
    IReadOnlyList<PurchaseOrderDraftLine> Lines) : IPurchaseOrderDraft;

internal sealed class CreatePurchaseOrderValidator : AbstractValidator<CreatePurchaseOrder>
{
    public CreatePurchaseOrderValidator(IClock clock)
    {
        Include(new PurchaseOrderDraftValidator(clock));
    }
}

public sealed class CreatePurchaseOrderHandler(
    IPurchasingDbContext db, IVatRateLookup vatRates, IValidator<CreatePurchaseOrder> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreatePurchaseOrder command, CancellationToken cancellationToken)
    {
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

        var order = PurchaseOrder.CreateDraft(
            command.CompanyId, command.SupplierId, command.OrderDate, command.ExpectedDate, command.WarehouseId, command.Note, lines.Value, clock.UtcNow);
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return order.Id;
    }
}
