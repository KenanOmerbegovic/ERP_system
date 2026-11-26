using Erp.Modules.Sales.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Deliveries;

/// <summary>Deletes a draft delivery note. A confirmed one is reversed with a storno instead.</summary>
public sealed record DeleteDeliveryNote(CompanyId CompanyId, Guid DeliveryNoteId);

public sealed class DeleteDeliveryNoteHandler(ISalesDbContext db, ISalesLocks locks)
{
    public async Task<Result> HandleAsync(DeleteDeliveryNote command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockDeliveryNoteAsync(command.CompanyId, command.DeliveryNoteId, cancellationToken))
        {
            return SalesErrors.DeliveryNoteNotFound;
        }

        var note = await db.DeliveryNotes.Include(d => d.Lines).FirstAsync(d => d.Id == command.DeliveryNoteId, cancellationToken);
        if (!note.IsDraft)
        {
            return SalesErrors.DeliveryNoteNotDraft;
        }

        db.DeliveryNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
