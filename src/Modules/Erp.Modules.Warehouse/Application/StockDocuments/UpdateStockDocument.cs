using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.StockDocuments;

/// <summary>Replaces a draft's content, lines included. A confirmed document cannot be changed (NFR-INT-01).</summary>
public sealed record UpdateStockDocument(
    CompanyId CompanyId,
    Guid DocumentId,
    StockDocumentType Type,
    DateOnly DocumentDate,
    Guid? SourceWarehouseId,
    Guid? TargetWarehouseId,
    string? Note,
    IReadOnlyList<StockLineInput> Lines) : IStockDocumentDraft;

internal sealed class UpdateStockDocumentValidator : AbstractValidator<UpdateStockDocument>
{
    public UpdateStockDocumentValidator(IClock clock)
    {
        Include(new StockDocumentDraftValidator(clock));
    }
}

public sealed class UpdateStockDocumentHandler(IWarehouseDbContext db, IStockLocks locks, IValidator<UpdateStockDocument> validator)
{
    public async Task<Result> HandleAsync(UpdateStockDocument command, CancellationToken cancellationToken)
    {
        // Locked like a confirmation, so a draft cannot change while it is being confirmed.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockDocumentAsync(command.CompanyId, command.DocumentId, cancellationToken))
        {
            return WarehouseErrors.StockDocumentNotFound;
        }

        var document = await db.StockDocuments
            .Include(d => d.Lines)
            .FirstAsync(d => d.CompanyId == command.CompanyId && d.Id == command.DocumentId, cancellationToken);
        if (!document.IsDraft)
        {
            return WarehouseErrors.StockDocumentNotDraft;
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (await StockDocumentDraftChecks.CheckAsync(db, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        document.UpdateDraft(command.Type, command.DocumentDate, command.SourceWarehouseId, command.TargetWarehouseId, command.Note, command.Lines);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
