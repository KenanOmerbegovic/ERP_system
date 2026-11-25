using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Domain;
using Erp.Modules.Warehouse.Domain.Numbering;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.Numbering;

/// <summary>
/// Adds a number series (configuration, effective-dated). Series are never edited: a new format starts with a new
/// series from a later date, so numbers already given keep their meaning.
/// </summary>
public sealed record CreateNumberSeries(CompanyId CompanyId, StockDocumentType DocumentType, DateOnly ValidFrom, string Prefix, int Padding);

internal sealed class CreateNumberSeriesValidator : AbstractValidator<CreateNumberSeries>
{
    public CreateNumberSeriesValidator()
    {
        RuleFor(x => x.DocumentType).IsInEnum();
        RuleFor(x => x.Prefix).NotEmpty().MaximumLength(FieldLengths.NumberPrefix);
        RuleFor(x => x.Padding).InclusiveBetween(NumberSeries.MinPadding, NumberSeries.MaxPadding);
    }
}

public sealed class CreateNumberSeriesHandler(IWarehouseDbContext db, IValidator<CreateNumberSeries> validator)
{
    public async Task<Result<Guid>> HandleAsync(CreateNumberSeries command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (await db.NumberSeries.AnyAsync(
                s => s.CompanyId == command.CompanyId && s.DocumentType == command.DocumentType && s.ValidFrom == command.ValidFrom,
                cancellationToken))
        {
            return WarehouseErrors.DuplicateNumberSeries;
        }

        var series = NumberSeries.Create(command.CompanyId, command.DocumentType, command.ValidFrom, command.Prefix, command.Padding);
        db.NumberSeries.Add(series);
        await db.SaveChangesAsync(cancellationToken);
        return series.Id;
    }
}
