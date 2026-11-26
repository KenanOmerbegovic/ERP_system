using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Receipts;

/// <summary>Deletes a draft receipt. A confirmed one is reversed with a storno.</summary>
public sealed record DeleteGoodsReceipt(CompanyId CompanyId, Guid ReceiptId);

public sealed class DeleteGoodsReceiptHandler(IPurchasingDbContext db, IPurchasingLocks locks)
{
    public async Task<Result> HandleAsync(DeleteGoodsReceipt command, CancellationToken cancellationToken)
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

        db.GoodsReceipts.Remove(receipt);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
