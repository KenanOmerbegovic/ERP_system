using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.UnitsOfMeasure;

public sealed record UpdateUnitOfMeasure(CompanyId CompanyId, Guid UnitOfMeasureId, string Code, string Name)
    : IUnitOfMeasureFields;

internal sealed class UpdateUnitOfMeasureValidator : AbstractValidator<UpdateUnitOfMeasure>
{
    public UpdateUnitOfMeasureValidator()
    {
        Include(new UnitOfMeasureFieldsValidator());
    }
}

public sealed class UpdateUnitOfMeasureHandler(ICoreDbContext db, IValidator<UpdateUnitOfMeasure> validator)
{
    public async Task<Result> HandleAsync(UpdateUnitOfMeasure command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var unit = await db.UnitsOfMeasure.FirstOrDefaultAsync(
            u => u.CompanyId == command.CompanyId && u.Id == command.UnitOfMeasureId,
            cancellationToken);
        if (unit is null)
        {
            return CoreErrors.UnitOfMeasureNotFound;
        }

        var code = command.Code.Trim();
        if (await db.UnitsOfMeasure.AnyAsync(
                u => u.CompanyId == command.CompanyId && u.Code == code && u.Id != command.UnitOfMeasureId,
                cancellationToken))
        {
            return CoreErrors.DuplicateUnitOfMeasureCode;
        }

        unit.Update(code, command.Name);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
