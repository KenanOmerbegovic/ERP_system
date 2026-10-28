using Erp.Modules.Core.Domain;
using FluentValidation;

namespace Erp.Modules.Core.Application.UnitsOfMeasure;

public interface IUnitOfMeasureFields
{
    string Code { get; }

    string Name { get; }
}

internal sealed class UnitOfMeasureFieldsValidator : AbstractValidator<IUnitOfMeasureFields>
{
    public UnitOfMeasureFieldsValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(FieldLengths.UnitCode);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLengths.UnitName);
    }
}
