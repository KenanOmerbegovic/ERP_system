using Erp.Modules.Warehouse.Domain;
using FluentValidation;

namespace Erp.Modules.Warehouse.Application.Storehouses;

public interface IWarehouseFields
{
    string Code { get; }

    string Name { get; }
}

internal sealed class WarehouseFieldsValidator : AbstractValidator<IWarehouseFields>
{
    public WarehouseFieldsValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(FieldLengths.Code);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLengths.Name);
    }
}
