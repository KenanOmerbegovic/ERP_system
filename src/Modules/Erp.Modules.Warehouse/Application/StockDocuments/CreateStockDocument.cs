using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Warehouse.Application.StockDocuments;

/// <summary>Saves a draft (FR-WH-01). Stock moves only when it is confirmed.</summary>
public sealed record CreateStockDocument(
    CompanyId CompanyId,
    StockDocumentType Type,
    DateOnly DocumentDate,
    Guid? SourceWarehouseId,
    Guid? TargetWarehouseId,
    string? Note,
    IReadOnlyList<StockLineInput> Lines) : IStockDocumentDraft;

internal sealed class CreateStockDocumentValidator : AbstractValidator<CreateStockDocument>
{
    public CreateStockDocumentValidator(IClock clock)
    {
        Include(new StockDocumentDraftValidator(clock));
    }
}

public sealed class CreateStockDocumentHandler(IWarehouseDbContext db, IValidator<CreateStockDocument> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateStockDocument command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (await StockDocumentDraftChecks.CheckAsync(db, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var document = StockDocument.CreateDraft(
            command.CompanyId,
            command.Type,
            command.DocumentDate,
            command.SourceWarehouseId,
            command.TargetWarehouseId,
            command.Note,
            command.Lines,
            clock.UtcNow);
        db.StockDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return document.Id;
    }
}
