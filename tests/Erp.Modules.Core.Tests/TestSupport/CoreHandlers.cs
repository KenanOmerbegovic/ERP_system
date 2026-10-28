using Erp.Modules.Core.Application.Articles;
using Erp.Modules.Core.Application.BusinessPartners;
using Erp.Modules.Core.Application.Companies;
using Erp.Modules.Core.Application.UnitsOfMeasure;
using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Core.Infrastructure;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Core.Tests.TestSupport;

/// <summary>Handlers wired to one test database, plus helpers that create prerequisite data through them.</summary>
internal sealed class CoreHandlers(CoreDbContext db)
{
    private static readonly FakeTaxIdentifierRules TaxRules = FakeTaxIdentifierRules.Instance;

    public CreateCompanyHandler CreateCompany { get; } = new(db, new CreateCompanyValidator(TaxRules));

    public UpdateCompanyHandler UpdateCompany { get; } = new(db, new UpdateCompanyValidator(TaxRules));

    public ListCompaniesHandler ListCompanies { get; } = new(db);

    public CreateBusinessPartnerHandler CreateBusinessPartner { get; } = new(db, new CreateBusinessPartnerValidator(TaxRules), TaxRules);

    public UpdateBusinessPartnerHandler UpdateBusinessPartner { get; } = new(db, new UpdateBusinessPartnerValidator(TaxRules), TaxRules);

    public ListBusinessPartnersHandler ListBusinessPartners { get; } = new(db, new ListBusinessPartnersValidator());

    public CreateUnitOfMeasureHandler CreateUnitOfMeasure { get; } = new(db, new CreateUnitOfMeasureValidator());

    public UpdateUnitOfMeasureHandler UpdateUnitOfMeasure { get; } = new(db, new UpdateUnitOfMeasureValidator());

    public ListUnitsOfMeasureHandler ListUnitsOfMeasure { get; } = new(db);

    public CreateArticleHandler CreateArticle { get; } = new(db, new CreateArticleValidator());

    public UpdateArticleHandler UpdateArticle { get; } = new(db, new UpdateArticleValidator());

    public ListArticlesHandler ListArticles { get; } = new(db, new ListArticlesValidator());

    public async Task<CompanyId> ACompanyAsync(string taxId = "4200000000001", string name = "Firma d.o.o.")
    {
        var result = await CreateCompany.HandleAsync(Commands.CreateCompany(taxId: taxId, name: name), CancellationToken.None);
        return result.Value;
    }

    public async Task<Guid> AUnitAsync(CompanyId companyId, string code = "kom", string name = "Komad")
    {
        var result = await CreateUnitOfMeasure.HandleAsync(new CreateUnitOfMeasure(companyId, code, name), CancellationToken.None);
        return result.Value;
    }

    public async Task<Guid> APartnerAsync(CompanyId companyId, string code = "K001", string name = "Kupac d.o.o.")
    {
        var result = await CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, code: code, name: name),
            CancellationToken.None);
        return result.Value;
    }

    public async Task<Guid> AnArticleAsync(CompanyId companyId, Guid unitId, string code = "A001", string name = "Artikal")
    {
        var result = await CreateArticle.HandleAsync(
            new CreateArticle(companyId, code, name, ArticleType.Goods, unitId, IsActive: true),
            CancellationToken.None);
        return result.Value;
    }
}
