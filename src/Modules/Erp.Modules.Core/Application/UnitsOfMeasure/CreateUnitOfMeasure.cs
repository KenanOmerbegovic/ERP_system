using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Domain.UnitsOfMeasure;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.UnitsOfMeasure;

public sealed record CreateUnitOfMeasure(CompanyId CompanyId, string Code, string Name) : IUnitOfMeasureFields;

internal sealed class CreateUnitOfMeasureValidator : AbstractValidator<CreateUnitOfMeasure>
{
    public CreateUnitOfMeasureValidator()
    {
        Include(new UnitOfMeasureFieldsValidator());
    }
}

public sealed class CreateUnitOfMeasureHandler(ICoreDbContext db, IValidator<CreateUnitOfMeasure> validator)
{
    public async Task<Result<Guid>> HandleAsync(CreateUnitOfMeasure command, CancellationToken cancellationToken)
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
        if (await db.UnitsOfMeasure.AnyAsync(u => u.CompanyId == command.CompanyId && u.Code == code, cancellationToken))
        {
            return CoreErrors.DuplicateUnitOfMeasureCode;
        }

        var unit = UnitOfMeasure.Create(command.CompanyId, code, command.Name);
        db.UnitsOfMeasure.Add(unit);
        await db.SaveChangesAsync(cancellationToken);
        return unit.Id;
    }
}
