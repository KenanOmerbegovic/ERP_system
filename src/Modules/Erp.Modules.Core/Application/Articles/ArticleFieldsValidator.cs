using Erp.Modules.Core.Domain;
using Erp.Modules.Core.Domain.Articles;
using FluentValidation;

namespace Erp.Modules.Core.Application.Articles;

public interface IArticleFields
{
    string Code { get; }

    string Name { get; }

    ArticleType Type { get; }

    Guid UnitOfMeasureId { get; }

    bool IsActive { get; }
}

internal sealed class ArticleFieldsValidator : AbstractValidator<IArticleFields>
{
    public ArticleFieldsValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(FieldLengths.Code);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLengths.Name);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.UnitOfMeasureId).NotEmpty();
    }
}
