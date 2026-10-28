using Erp.Modules.Core.Application;
using Erp.Modules.Core.Application.Articles;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Core.Tests.TestSupport;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Tests.Application;

public sealed class ArticleTests : IDisposable
{
    private readonly CoreTestDatabase _database = new();
    private readonly CoreHandlers _handlers;

    public ArticleTests()
    {
        _handlers = new CoreHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Create_saves_article_with_unit()
    {
        var companyId = await _handlers.ACompanyAsync();
        var unitId = await _handlers.AUnitAsync(companyId);

        var result = await _handlers.CreateArticle.HandleAsync(
            new CreateArticle(companyId, " A001 ", "Vijak M8", ArticleType.Goods, unitId, IsActive: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var article = await _database.Query().Articles.SingleAsync();
        Assert.Equal(companyId, article.CompanyId);
        Assert.Equal("A001", article.Code);
        Assert.Equal(ArticleType.Goods, article.Type);
        Assert.Equal(unitId, article.UnitOfMeasureId);
        Assert.True(article.IsActive);
    }

    [Fact]
    public async Task Create_rejects_unit_of_another_company()
    {
        var companyId = await _handlers.ACompanyAsync(taxId: "4200000000001");
        var otherId = await _handlers.ACompanyAsync(taxId: "4200000000002");
        var foreignUnitId = await _handlers.AUnitAsync(otherId);

        var result = await _handlers.CreateArticle.HandleAsync(
            new CreateArticle(companyId, "A001", "Vijak", ArticleType.Goods, foreignUnitId, IsActive: true),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.ArticleUnitOfMeasureNotFound);
    }

    [Fact]
    public async Task Create_validates_input()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateArticle.HandleAsync(
            new CreateArticle(companyId, "", "", (ArticleType)99, Guid.Empty, IsActive: true),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "Code", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "Name", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "Type", "EnumValidator");
        ResultAssert.FailsValidation(result, "UnitOfMeasureId", "NotEmptyValidator");
    }

    [Fact]
    public async Task Create_in_unknown_company_is_not_found()
    {
        var result = await _handlers.CreateArticle.HandleAsync(
            new CreateArticle(CompanyId.New(), "A001", "Vijak", ArticleType.Goods, Guid.NewGuid(), IsActive: true),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.CompanyNotFound);
    }

    [Fact]
    public async Task Create_rejects_duplicate_code_in_same_company()
    {
        var companyId = await _handlers.ACompanyAsync();
        var unitId = await _handlers.AUnitAsync(companyId);
        await _handlers.AnArticleAsync(companyId, unitId, code: "A001");

        var result = await _handlers.CreateArticle.HandleAsync(
            new CreateArticle(companyId, "A001", "Drugi", ArticleType.Service, unitId, IsActive: true),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.DuplicateArticleCode);
    }

    [Fact]
    public async Task Update_changes_fields()
    {
        var companyId = await _handlers.ACompanyAsync();
        var komId = await _handlers.AUnitAsync(companyId, code: "kom");
        var hId = await _handlers.AUnitAsync(companyId, code: "h", name: "Sat");
        var articleId = await _handlers.AnArticleAsync(companyId, komId);

        var result = await _handlers.UpdateArticle.HandleAsync(
            new UpdateArticle(companyId, articleId, "U001", "Montaža", ArticleType.Service, hId, IsActive: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var article = await _database.Query().Articles.SingleAsync();
        Assert.Equal("U001", article.Code);
        Assert.Equal("Montaža", article.Name);
        Assert.Equal(ArticleType.Service, article.Type);
        Assert.Equal(hId, article.UnitOfMeasureId);
        Assert.False(article.IsActive);
    }

    [Fact]
    public async Task Update_rejects_unit_of_another_company()
    {
        var companyId = await _handlers.ACompanyAsync(taxId: "4200000000001");
        var otherId = await _handlers.ACompanyAsync(taxId: "4200000000002");
        var unitId = await _handlers.AUnitAsync(companyId);
        var foreignUnitId = await _handlers.AUnitAsync(otherId);
        var articleId = await _handlers.AnArticleAsync(companyId, unitId);

        var result = await _handlers.UpdateArticle.HandleAsync(
            new UpdateArticle(companyId, articleId, "A001", "Artikal", ArticleType.Goods, foreignUnitId, IsActive: true),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.ArticleUnitOfMeasureNotFound);
    }

    [Fact]
    public async Task Update_rejects_code_of_another_article()
    {
        var companyId = await _handlers.ACompanyAsync();
        var unitId = await _handlers.AUnitAsync(companyId);
        await _handlers.AnArticleAsync(companyId, unitId, code: "A001");
        var secondId = await _handlers.AnArticleAsync(companyId, unitId, code: "A002");

        var result = await _handlers.UpdateArticle.HandleAsync(
            new UpdateArticle(companyId, secondId, "A001", "Artikal", ArticleType.Goods, unitId, IsActive: true),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.DuplicateArticleCode);
    }

    [Fact]
    public async Task Update_cannot_reach_article_of_another_company()
    {
        var owner = await _handlers.ACompanyAsync(taxId: "4200000000001");
        var other = await _handlers.ACompanyAsync(taxId: "4200000000002");
        var ownerUnit = await _handlers.AUnitAsync(owner);
        var otherUnit = await _handlers.AUnitAsync(other);
        var articleId = await _handlers.AnArticleAsync(owner, ownerUnit);

        var result = await _handlers.UpdateArticle.HandleAsync(
            new UpdateArticle(other, articleId, "A001", "Artikal", ArticleType.Goods, otherUnit, IsActive: true),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.ArticleNotFound);
    }

    [Fact]
    public async Task Update_validates_input()
    {
        var companyId = await _handlers.ACompanyAsync();
        var unitId = await _handlers.AUnitAsync(companyId);
        var articleId = await _handlers.AnArticleAsync(companyId, unitId);

        var result = await _handlers.UpdateArticle.HandleAsync(
            new UpdateArticle(companyId, articleId, "A001", new string('x', 201), ArticleType.Goods, unitId, IsActive: true),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "Name", "MaximumLengthValidator");
    }

    [Fact]
    public async Task List_returns_company_articles_with_search_and_paging()
    {
        var companyId = await _handlers.ACompanyAsync(taxId: "4200000000001");
        var otherId = await _handlers.ACompanyAsync(taxId: "4200000000002");
        var unitId = await _handlers.AUnitAsync(companyId);
        var otherUnitId = await _handlers.AUnitAsync(otherId);
        await _handlers.AnArticleAsync(companyId, unitId, code: "A003", name: "Vijak M10");
        await _handlers.AnArticleAsync(companyId, unitId, code: "A001", name: "Vijak M8");
        await _handlers.AnArticleAsync(companyId, unitId, code: "A002", name: "Matica M8");
        await _handlers.AnArticleAsync(otherId, otherUnitId, code: "A000", name: "Vijak tuđi");

        var result = await _handlers.ListArticles.HandleAsync(
            new ListArticles(companyId, "VIJAK", Page: 1, PageSize: 1),
            CancellationToken.None);

        var article = Assert.Single(result.Value.Items);
        Assert.Equal("A001", article.Code);
        Assert.Equal(ArticleKind.Goods, article.Type);
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task List_of_unknown_company_is_not_found()
    {
        var result = await _handlers.ListArticles.HandleAsync(
            new ListArticles(CompanyId.New(), null, 1, 50),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.CompanyNotFound);
    }
}
