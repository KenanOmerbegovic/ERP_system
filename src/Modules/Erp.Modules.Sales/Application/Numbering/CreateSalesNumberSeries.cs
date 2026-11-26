using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Numbering;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Numbering;

/// <summary>Adds a number series (configuration, effective-dated); a new format is a new series from a later date.</summary>
public sealed record CreateSalesNumberSeries(CompanyId CompanyId, SalesDocumentType DocumentType, DateOnly ValidFrom, string Prefix, int Padding);

internal sealed class CreateSalesNumberSeriesValidator : AbstractValidator<CreateSalesNumberSeries>
{
    public CreateSalesNumberSeriesValidator()
    {
        RuleFor(x => x.DocumentType).IsInEnum();
        RuleFor(x => x.Prefix).NotEmpty().MaximumLength(FieldLengths.NumberPrefix);
        RuleFor(x => x.Padding).InclusiveBetween(NumberSeries.MinPadding, NumberSeries.MaxPadding);
    }
}

public sealed class CreateSalesNumberSeriesHandler(ISalesDbContext db, IValidator<CreateSalesNumberSeries> validator)
{
    public async Task<Result<Guid>> HandleAsync(CreateSalesNumberSeries command, CancellationToken cancellationToken)
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
            return SalesErrors.DuplicateNumberSeries;
        }

        var series = NumberSeries.Create(command.CompanyId, command.DocumentType, command.ValidFrom, command.Prefix, command.Padding);
        db.NumberSeries.Add(series);
        await db.SaveChangesAsync(cancellationToken);
        return series.Id;
    }
}
