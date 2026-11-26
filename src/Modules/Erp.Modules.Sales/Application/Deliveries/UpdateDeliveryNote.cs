using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Deliveries;

/// <summary>Replaces a draft delivery note's content; it stays with its order.</summary>
public sealed record UpdateDeliveryNote(
    CompanyId CompanyId,
    Guid DeliveryNoteId,
    Guid SalesOrderId,
    Guid WarehouseId,
    DateOnly DeliveryDate,
    string? Note,
    IReadOnlyList<DeliveryNoteLineInput> Lines) : IDeliveryNoteDraft;

internal sealed class UpdateDeliveryNoteValidator : AbstractValidator<UpdateDeliveryNote>
{
    public UpdateDeliveryNoteValidator(IClock clock)
    {
        Include(new DeliveryNoteDraftValidator(clock));
    }
}

public sealed class UpdateDeliveryNoteHandler(ISalesDbContext db, ISalesLocks locks, IValidator<UpdateDeliveryNote> validator)
{
    public async Task<Result> HandleAsync(UpdateDeliveryNote command, CancellationToken cancellationToken)
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

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (command.SalesOrderId != note.SalesOrderId)
        {
            var errors = new ValidationErrors();
            errors.Add(nameof(command.SalesOrderId), "DeliveryNote.OrderFixed");
            return errors.ToError();
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, note.SalesOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        if (await DeliveryNoteRules.CheckAsync(db, order.Value, note.Id, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        note.UpdateDraft(order.Value, command.WarehouseId, command.DeliveryDate, command.Note, command.Lines);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
