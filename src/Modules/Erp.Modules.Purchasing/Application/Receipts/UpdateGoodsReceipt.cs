using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Receipts;

/// <summary>Replaces a draft receipt's content; it stays with its order. Not possible once confirmation started.</summary>
public sealed record UpdateGoodsReceipt(
    CompanyId CompanyId,
    Guid ReceiptId,
    Guid PurchaseOrderId,
    Guid WarehouseId,
    DateOnly ReceiptDate,
    string? Note,
    IReadOnlyList<GoodsReceiptLineInput> Lines) : IGoodsReceiptDraft;

internal sealed class UpdateGoodsReceiptValidator : AbstractValidator<UpdateGoodsReceipt>
{
    public UpdateGoodsReceiptValidator(IClock clock)
    {
        Include(new GoodsReceiptDraftValidator(clock));
    }
}

public sealed class UpdateGoodsReceiptHandler(IPurchasingDbContext db, IPurchasingLocks locks, IValidator<UpdateGoodsReceipt> validator)
{
    public async Task<Result> HandleAsync(UpdateGoodsReceipt command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockReceiptAsync(command.CompanyId, command.ReceiptId, cancellationToken))
        {
            return PurchasingErrors.ReceiptNotFound;
        }

        var receipt = await db.GoodsReceipts.Include(r => r.Lines).FirstAsync(r => r.Id == command.ReceiptId, cancellationToken);
        if (!receipt.IsDraft)
        {
            return PurchasingErrors.ReceiptNotDraft;
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (command.PurchaseOrderId != receipt.PurchaseOrderId)
        {
            var errors = new ValidationErrors();
            errors.Add(nameof(command.PurchaseOrderId), "GoodsReceipt.OrderCannotChange");
            return errors.ToError();
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, command.PurchaseOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        if (await GoodsReceiptRules.CheckAsync(db, order.Value, receipt.Id, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        receipt.UpdateDraft(order.Value, command.WarehouseId, command.ReceiptDate, command.Note, command.Lines);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
