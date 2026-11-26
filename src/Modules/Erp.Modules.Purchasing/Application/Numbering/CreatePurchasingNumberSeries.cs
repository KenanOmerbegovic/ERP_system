using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Contracts.Numbering;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Numbering;

/// <summary>Adds a number series (configuration, effective-dated); a new format is a new series from a later date.</summary>
public sealed record CreatePurchasingNumberSeries(CompanyId CompanyId, PurchasingDocumentType DocumentType, DateOnly ValidFrom, string Prefix, int Padding);

internal sealed class CreatePurchasingNumberSeriesValidator : AbstractValidator<CreatePurchasingNumberSeries>
{
    public CreatePurchasingNumberSeriesValidator()
    {
        RuleFor(x => x.DocumentType).IsInEnum();
        RuleFor(x => x.Prefix).NotEmpty().MaximumLength(FieldLengths.NumberPrefix);
        RuleFor(x => x.Padding).InclusiveBetween(NumberSeries.MinPadding, NumberSeries.MaxPadding);
    }
}

public sealed class CreatePurchasingNumberSeriesHandler(IPurchasingDbContext db, IValidator<CreatePurchasingNumberSeries> validator)
{
    public async Task<Result<Guid>> HandleAsync(CreatePurchasingNumberSeries command, CancellationToken cancellationToken)
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
            return PurchasingErrors.DuplicateNumberSeries;
        }

        var series = NumberSeries.Create(command.CompanyId, command.DocumentType, command.ValidFrom, command.Prefix, command.Padding);
        db.NumberSeries.Add(series);
        await db.SaveChangesAsync(cancellationToken);
        return series.Id;
    }
}
