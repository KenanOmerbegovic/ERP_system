using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Domain;
using Erp.Modules.Core.Domain.Vat;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Vat;

/// <summary>Adds a VAT rate (configuration, effective-dated). A rate change is a new rate of the same code from a later date.</summary>
public sealed record CreateVatRate(CompanyId CompanyId, string Code, string Name, decimal Rate, DateOnly ValidFrom);

internal sealed class CreateVatRateValidator : AbstractValidator<CreateVatRate>
{
    public CreateVatRateValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(FieldLengths.VatCode);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLengths.UnitName);
        RuleFor(x => x.Rate).InclusiveBetween(0m, VatRate.MaxRate).PrecisionScale(5, 4, ignoreTrailingZeros: true);
    }
}

public sealed class CreateVatRateHandler(ICoreDbContext db, IValidator<CreateVatRate> validator)
{
    public async Task<Result<Guid>> HandleAsync(CreateVatRate command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (!await db.CompanyExistsAsync(command.CompanyId, cancellationToken))
        {
            return CoreErrors.CompanyNotFound;
        }

        var code = command.Code.Trim();
        if (await db.VatRates.AnyAsync(
                r => r.CompanyId == command.CompanyId && r.Code == code && r.ValidFrom == command.ValidFrom,
                cancellationToken))
        {
            return CoreErrors.DuplicateVatRate;
        }

        var rate = VatRate.Create(command.CompanyId, code, command.Name, command.Rate, command.ValidFrom);
        db.VatRates.Add(rate);
        await db.SaveChangesAsync(cancellationToken);
        return rate.Id;
    }
}
