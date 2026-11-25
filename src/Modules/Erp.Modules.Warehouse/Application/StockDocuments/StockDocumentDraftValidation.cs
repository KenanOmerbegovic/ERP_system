using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Domain;
using Erp.Modules.Warehouse.Domain.Articles;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.StockDocuments;

/// <summary>What a draft says; shared by create and update.</summary>
public interface IStockDocumentDraft
{
    CompanyId CompanyId { get; }

    StockDocumentType Type { get; }

    DateOnly DocumentDate { get; }

    Guid? SourceWarehouseId { get; }

    Guid? TargetWarehouseId { get; }

    string? Note { get; }

    IReadOnlyList<StockLineInput> Lines { get; }
}

/// <summary>The shape of a draft: warehouses per type, dates, quantities, costs.</summary>
internal sealed class StockDocumentDraftValidator : AbstractValidator<IStockDocumentDraft>
{
    public const int MaxLines = 500;

    public StockDocumentDraftValidator(IClock clock)
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.DocumentDate)
            .Must(date => date <= clock.Today)
            .WithErrorCode(WarehouseValidationCodes.DocumentDateInFuture)
            .WithMessage("The document date cannot be in the future.");
        RuleFor(x => x.Note).MaximumLength(FieldLengths.Note);

        RuleFor(x => x.SourceWarehouseId).NotEmpty().When(x => x.Type is StockDocumentType.Issue or StockDocumentType.Transfer);
        RuleFor(x => x.SourceWarehouseId).Null().WithErrorCode(WarehouseValidationCodes.NotAllowed).When(x => x.Type == StockDocumentType.Receipt);
        RuleFor(x => x.TargetWarehouseId).NotEmpty().When(x => x.Type is StockDocumentType.Receipt or StockDocumentType.Transfer);
        RuleFor(x => x.TargetWarehouseId).Null().WithErrorCode(WarehouseValidationCodes.NotAllowed).When(x => x.Type == StockDocumentType.Issue);
        RuleFor(x => x.TargetWarehouseId)
            .NotEqual(x => x.SourceWarehouseId)
            .WithErrorCode(WarehouseValidationCodes.SameWarehouse)
            .When(x => x.Type == StockDocumentType.Transfer && x.SourceWarehouseId is not null);

        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= MaxLines).WithErrorCode("Lines.TooMany");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ArticleId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0m).PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
            line.RuleFor(l => l.LotNumber).MaximumLength(FieldLengths.LotNumber);
            line.RuleForEach(l => l.SerialNumbers).NotEmpty().MaximumLength(FieldLengths.SerialNumber);
        });
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.UnitCost).NotNull().GreaterThanOrEqualTo(0m)
                .PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
        }).When(x => x.Type == StockDocumentType.Receipt);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.UnitCost).Null().WithErrorCode(WarehouseValidationCodes.NotAllowed);
            line.RuleFor(l => l.LotExpiryDate).Null().WithErrorCode(WarehouseValidationCodes.NotAllowed);
        }).When(x => x.Type != StockDocumentType.Receipt);
    }
}

/// <summary>Checks a valid-shaped draft against stored data: its warehouses and articles, and each article's tracking type.</summary>
internal static class StockDocumentDraftChecks
{
    public static async Task<ValidationError?> CheckAsync(IWarehouseDbContext db, IStockDocumentDraft draft, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        await CheckWarehouseAsync(db, draft.CompanyId, draft.SourceWarehouseId, nameof(draft.SourceWarehouseId), errors, cancellationToken);
        await CheckWarehouseAsync(db, draft.CompanyId, draft.TargetWarehouseId, nameof(draft.TargetWarehouseId), errors, cancellationToken);

        var articleIds = draft.Lines.Select(line => line.ArticleId).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking()
            .Where(a => a.CompanyId == draft.CompanyId && articleIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var serialsSeen = new HashSet<(Guid, string)>();
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var prefix = $"{nameof(draft.Lines)}[{index}]";
            if (!articles.TryGetValue(line.ArticleId, out var article))
            {
                errors.Add($"{prefix}.{nameof(line.ArticleId)}", WarehouseValidationCodes.ArticleNotFound);
                continue;
            }

            if (!article.IsStockable)
            {
                errors.Add($"{prefix}.{nameof(line.ArticleId)}", WarehouseValidationCodes.ArticleNotStockable);
            }

            CheckTracking(article.Tracking, line, prefix, errors);
            foreach (var serial in line.SerialNumbers)
            {
                if (!serialsSeen.Add((line.ArticleId, serial.Trim())))
                {
                    errors.Add($"{prefix}.{nameof(line.SerialNumbers)}", WarehouseValidationCodes.SerialNumberDuplicate);
                }
            }
        }

        return errors.Any ? errors.ToError() : null;
    }

    private static void CheckTracking(TrackingType tracking, StockLineInput line, string prefix, ValidationErrors errors)
    {
        var hasLot = !string.IsNullOrWhiteSpace(line.LotNumber);
        var hasSerials = line.SerialNumbers.Count > 0;
        if (tracking == TrackingType.Lot && !hasLot)
        {
            errors.Add($"{prefix}.{nameof(line.LotNumber)}", WarehouseValidationCodes.LotNumberRequired);
        }

        if (tracking != TrackingType.Lot && (hasLot || line.LotExpiryDate is not null))
        {
            errors.Add($"{prefix}.{nameof(line.LotNumber)}", WarehouseValidationCodes.LotNumberNotAllowed);
        }

        if (tracking == TrackingType.Serial && line.SerialNumbers.Count != line.Quantity)
        {
            errors.Add($"{prefix}.{nameof(line.SerialNumbers)}", WarehouseValidationCodes.SerialNumberCountMismatch);
        }

        if (tracking != TrackingType.Serial && hasSerials)
        {
            errors.Add($"{prefix}.{nameof(line.SerialNumbers)}", WarehouseValidationCodes.SerialNumbersNotAllowed);
        }
    }

    private static async Task CheckWarehouseAsync(
        IWarehouseDbContext db, CompanyId companyId, Guid? warehouseId, string property, ValidationErrors errors, CancellationToken cancellationToken)
    {
        if (warehouseId is not { } id)
        {
            return;
        }

        var storehouse = await db.Storehouses.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Id == id, cancellationToken);
        if (storehouse is null)
        {
            errors.Add(property, WarehouseValidationCodes.WarehouseNotFound);
        }
        else if (!storehouse.IsActive)
        {
            errors.Add(property, WarehouseValidationCodes.WarehouseInactive);
        }
    }
}
