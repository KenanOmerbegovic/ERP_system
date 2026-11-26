using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Sales.Application.Deliveries;

/// <summary>Saves a draft delivery note against a confirmed order (FR-SAL-03). Stock leaves when it is confirmed.</summary>
public sealed record CreateDeliveryNote(
    CompanyId CompanyId,
    Guid SalesOrderId,
    Guid WarehouseId,
    DateOnly DeliveryDate,
    string? Note,
    IReadOnlyList<DeliveryNoteLineInput> Lines) : IDeliveryNoteDraft;

internal sealed class CreateDeliveryNoteValidator : AbstractValidator<CreateDeliveryNote>
{
    public CreateDeliveryNoteValidator(IClock clock)
    {
        Include(new DeliveryNoteDraftValidator(clock));
    }
}

public sealed class CreateDeliveryNoteHandler(ISalesDbContext db, IValidator<CreateDeliveryNote> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateDeliveryNote command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, command.SalesOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        if (await DeliveryNoteRules.CheckAsync(db, order.Value, null, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var note = DeliveryNote.CreateDraft(command.CompanyId, order.Value, command.WarehouseId, command.DeliveryDate, command.Note, command.Lines, clock.UtcNow);
        db.DeliveryNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return note.Id;
    }
}
