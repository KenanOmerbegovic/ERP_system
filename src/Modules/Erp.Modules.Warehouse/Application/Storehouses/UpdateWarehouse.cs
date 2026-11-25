using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.Storehouses;

public sealed record UpdateWarehouse(CompanyId CompanyId, Guid WarehouseId, string Code, string Name, bool IsActive) : IWarehouseFields;

internal sealed class UpdateWarehouseValidator : AbstractValidator<UpdateWarehouse>
{
    public UpdateWarehouseValidator()
    {
        Include(new WarehouseFieldsValidator());
    }
}

public sealed class UpdateWarehouseHandler(IWarehouseDbContext db, IValidator<UpdateWarehouse> validator)
{
    public async Task<Result> HandleAsync(UpdateWarehouse command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var storehouse = await db.Storehouses.FirstOrDefaultAsync(
            s => s.CompanyId == command.CompanyId && s.Id == command.WarehouseId,
            cancellationToken);
        if (storehouse is null)
        {
            return WarehouseErrors.WarehouseNotFound;
        }

        var code = command.Code.Trim();
        if (await db.Storehouses.AnyAsync(
                s => s.CompanyId == command.CompanyId && s.Code == code && s.Id != command.WarehouseId,
                cancellationToken))
        {
            return WarehouseErrors.DuplicateWarehouseCode;
        }

        storehouse.Update(code, command.Name, command.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
