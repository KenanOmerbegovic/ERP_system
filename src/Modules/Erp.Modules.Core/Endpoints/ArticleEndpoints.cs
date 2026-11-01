using Erp.Modules.Core.Application.Articles;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Core.Endpoints;

internal static class ArticleEndpoints
{
    public static void MapArticleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/articles").WithTags("Articles");

        group.MapGet("/", async (
            Guid companyId,
            string? search,
            int? page,
            int? pageSize,
            ListArticlesHandler handler,
            CancellationToken cancellationToken) =>
            HttpResults.Ok(await handler.HandleAsync(
                new ListArticles(new CompanyId(companyId), search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                cancellationToken)))
            .WithName("ListArticles")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.ArticlesRead))
            .Produces<PagedResponse<ArticleResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            Guid companyId,
            ArticleRequest request,
            CreateArticleHandler handler,
            CancellationToken cancellationToken) =>
            HttpResults.Created(await handler.HandleAsync(
                new CreateArticle(
                    new CompanyId(companyId),
                    request.Code,
                    request.Name,
                    ToType(request.Type),
                    request.UnitOfMeasureId,
                    request.IsActive),
                cancellationToken)))
            .WithName("CreateArticle")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.ArticlesWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{articleId:guid}", async (
            Guid companyId,
            Guid articleId,
            ArticleRequest request,
            UpdateArticleHandler handler,
            CancellationToken cancellationToken) =>
            HttpResults.NoContent(await handler.HandleAsync(
                new UpdateArticle(
                    new CompanyId(companyId),
                    articleId,
                    request.Code,
                    request.Name,
                    ToType(request.Type),
                    request.UnitOfMeasureId,
                    request.IsActive),
                cancellationToken)))
            .WithName("UpdateArticle")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.ArticlesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // Undefined values pass through unchanged so the validator reports them.
    private static ArticleType ToType(ArticleKind kind) => kind switch
    {
        ArticleKind.Goods => ArticleType.Goods,
        ArticleKind.Service => ArticleType.Service,
        _ => (ArticleType)(int)kind,
    };
}
