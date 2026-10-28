using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Domain.Articles;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Articles;

/// <summary>Articles ordered by code; <see cref="Search"/> matches code or name, case-insensitively.</summary>
public sealed record ListArticles(CompanyId CompanyId, string? Search, int Page, int PageSize);

internal sealed class ListArticlesValidator : AbstractValidator<ListArticles>
{
    public ListArticlesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListArticlesHandler(ICoreDbContext db, IValidator<ListArticles> validator)
{
    public async Task<Result<PagedResponse<ArticleResponse>>> HandleAsync(ListArticles query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (!await db.CompanyExistsAsync(query.CompanyId, cancellationToken))
        {
            return CoreErrors.CompanyNotFound;
        }

        var articles = db.Articles.AsNoTracking().Where(a => a.CompanyId == query.CompanyId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            articles = articles.Where(a => a.Code.ToLower().Contains(search) || a.Name.ToLower().Contains(search));
        }

        var totalCount = await articles.CountAsync(cancellationToken);
        var page = await articles
            .OrderBy(a => a.Code)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(a => new ArticleResponse(a.Id, a.Code, a.Name, ToKind(a.Type), a.UnitOfMeasureId, a.IsActive))
            .ToList();
        return new PagedResponse<ArticleResponse>(items, query.Page, query.PageSize, totalCount);
    }

    private static ArticleKind ToKind(ArticleType type) => type switch
    {
        ArticleType.Goods => ArticleKind.Goods,
        ArticleType.Service => ArticleKind.Service,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
