using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Domain.Articles;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Articles;

public sealed record CreateArticle(
    CompanyId CompanyId,
    string Code,
    string Name,
    ArticleType Type,
    Guid UnitOfMeasureId,
    bool IsActive) : IArticleFields;

internal sealed class CreateArticleValidator : AbstractValidator<CreateArticle>
{
    public CreateArticleValidator()
    {
        Include(new ArticleFieldsValidator());
    }
}

public sealed class CreateArticleHandler(ICoreDbContext db, IValidator<CreateArticle> validator)
{
    public async Task<Result<Guid>> HandleAsync(CreateArticle command, CancellationToken cancellationToken)
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

        if (!await db.UnitsOfMeasure.AnyAsync(
                u => u.CompanyId == command.CompanyId && u.Id == command.UnitOfMeasureId,
                cancellationToken))
        {
            return CoreErrors.ArticleUnitOfMeasureNotFound;
        }

        var code = command.Code.Trim();
        if (await db.Articles.AnyAsync(a => a.CompanyId == command.CompanyId && a.Code == code, cancellationToken))
        {
            return CoreErrors.DuplicateArticleCode;
        }

        var article = Article.Create(command.CompanyId, code, command.Name, command.Type, command.UnitOfMeasureId, command.IsActive);
        db.Articles.Add(article);
        await db.SaveChangesAsync(cancellationToken);
        return article.Id;
    }
}
