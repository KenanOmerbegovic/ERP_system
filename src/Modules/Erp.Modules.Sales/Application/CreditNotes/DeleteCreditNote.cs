using Erp.Modules.Sales.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.CreditNotes;

/// <summary>Deletes a draft credit note. A confirmed one is reversed with a storno instead.</summary>
public sealed record DeleteCreditNote(CompanyId CompanyId, Guid CreditNoteId);

public sealed class DeleteCreditNoteHandler(ISalesDbContext db, ISalesLocks locks)
{
    public async Task<Result> HandleAsync(DeleteCreditNote command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockCreditNoteAsync(command.CompanyId, command.CreditNoteId, cancellationToken))
        {
            return SalesErrors.CreditNoteNotFound;
        }

        var creditNote = await db.CreditNotes.Include(c => c.Lines).Include(c => c.VatTotals).AsSplitQuery()
            .FirstAsync(c => c.Id == command.CreditNoteId, cancellationToken);
        if (!creditNote.IsDraft)
        {
            return SalesErrors.CreditNoteNotDraft;
        }

        db.CreditNotes.Remove(creditNote);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
