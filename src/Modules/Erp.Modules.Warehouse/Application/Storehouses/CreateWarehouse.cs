using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Domain.Storehouses;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.Storehouses;

public sealed record CreateWarehouse(CompanyId CompanyId, string Code, string Name, bool IsActive) : IWarehouseFields;

internal sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouse>
{
    public CreateWarehouseValidator()
    {
        Include(new WarehouseFieldsValidator());
    }
}

public sealed class CreateWarehouseHandler(IWarehouseDbContext db, IValidator<CreateWarehouse> validator)
{
    public async Task<Result<Guid>> HandleAsync(CreateWarehouse command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var code = command.Code.Trim();
        if (await db.Storehouses.AnyAsync(s => s.CompanyId == command.CompanyId && s.Code == code, cancellationToken))
        {
            return WarehouseErrors.DuplicateWarehouseCode;
        }

        var storehouse = Storehouse.Create(command.CompanyId, code, command.Name, command.IsActive);
        db.Storehouses.Add(storehouse);
        await db.SaveChangesAsync(cancellationToken);
        return storehouse.Id;
    }
}
