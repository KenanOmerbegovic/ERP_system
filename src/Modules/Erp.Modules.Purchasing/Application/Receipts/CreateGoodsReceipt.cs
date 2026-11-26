using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Purchasing.Application.Receipts;

/// <summary>Saves a draft goods receipt against a confirmed order (FR-PUR-01). Stock moves when it is confirmed.</summary>
public sealed record CreateGoodsReceipt(
    CompanyId CompanyId,
    Guid PurchaseOrderId,
    Guid WarehouseId,
    DateOnly ReceiptDate,
    string? Note,
    IReadOnlyList<GoodsReceiptLineInput> Lines) : IGoodsReceiptDraft;

internal sealed class CreateGoodsReceiptValidator : AbstractValidator<CreateGoodsReceipt>
{
    public CreateGoodsReceiptValidator(IClock clock)
    {
        Include(new GoodsReceiptDraftValidator(clock));
    }
}

public sealed class CreateGoodsReceiptHandler(IPurchasingDbContext db, IValidator<CreateGoodsReceipt> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateGoodsReceipt command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, command.PurchaseOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        if (await GoodsReceiptRules.CheckAsync(db, order.Value, null, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var receipt = GoodsReceipt.CreateDraft(
            command.CompanyId, order.Value, command.WarehouseId, command.ReceiptDate, command.Note, command.Lines, clock.UtcNow);
        db.GoodsReceipts.Add(receipt);
        await db.SaveChangesAsync(cancellationToken);
        return receipt.Id;
    }
}
