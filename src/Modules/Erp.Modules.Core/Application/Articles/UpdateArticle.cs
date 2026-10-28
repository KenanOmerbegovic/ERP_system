using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Domain.Articles;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Articles;

public sealed record UpdateArticle(
    CompanyId CompanyId,
    Guid ArticleId,
    string Code,
    string Name,
    ArticleType Type,
    Guid UnitOfMeasureId,
    bool IsActive) : IArticleFields;

internal sealed class UpdateArticleValidator : AbstractValidator<UpdateArticle>
{
    public UpdateArticleValidator()
    {
        Include(new ArticleFieldsValidator());
    }
}

public sealed class UpdateArticleHandler(ICoreDbContext db, IValidator<UpdateArticle> validator)
{
    public async Task<Result> HandleAsync(UpdateArticle command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var article = await db.Articles.FirstOrDefaultAsync(
            a => a.CompanyId == command.CompanyId && a.Id == command.ArticleId,
            cancellationToken);
        if (article is null)
        {
            return CoreErrors.ArticleNotFound;
        }

        if (!await db.UnitsOfMeasure.AnyAsync(
                u => u.CompanyId == command.CompanyId && u.Id == command.UnitOfMeasureId,
                cancellationToken))
        {
            return CoreErrors.ArticleUnitOfMeasureNotFound;
        }

        var code = command.Code.Trim();
        if (await db.Articles.AnyAsync(
                a => a.CompanyId == command.CompanyId && a.Code == code && a.Id != command.ArticleId,
                cancellationToken))
        {
            return CoreErrors.DuplicateArticleCode;
        }

        article.Update(code, command.Name, command.Type, command.UnitOfMeasureId, command.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
