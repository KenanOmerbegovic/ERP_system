using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Core.Domain.BusinessPartners;
using Erp.Modules.Core.Domain.Companies;
using Erp.Modules.Core.Domain.UnitsOfMeasure;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Core.Tests.Domain;

/// <summary>Domain guards: input is validated earlier, so invalid values here are bugs and throw.</summary>
public class CoreDomainTests
{
    private static readonly CompanyId Tenant = CompanyId.New();

    [Fact]
    public void Company_is_its_own_tenant()
    {
        var company = Company.Create("Firma", "4200000000001", null, "Ferhadija 1", "71000", "Sarajevo");

        Assert.Equal(company.Id, company.CompanyId);
        Assert.False(company.IsVatRegistered);
    }

    [Fact]
    public void Company_requires_name()
    {
        Assert.Throws<ArgumentException>(() => Company.Create(" ", "4200000000001", null, "Ferhadija 1", "71000", "Sarajevo"));
    }

    [Fact]
    public void Business_partner_normalizes_text()
    {
        var partner = BusinessPartner.Create(Tenant, new BusinessPartnerDetails(
            " K001 ", " Kupac ", " ", null, "", null, null, "ba", IsCustomer: true, IsSupplier: false, IsActive: true));

        Assert.Equal("K001", partner.Code);
        Assert.Equal("Kupac", partner.Name);
        Assert.Null(partner.TaxId);
        Assert.Null(partner.Address);
        Assert.Equal("BA", partner.CountryCode);
    }

    [Fact]
    public void Business_partner_requires_country()
    {
        Assert.Throws<ArgumentException>(() => BusinessPartner.Create(Tenant, new BusinessPartnerDetails(
            "K001", "Kupac", null, null, null, null, null, "", IsCustomer: true, IsSupplier: false, IsActive: true)));
    }

    [Fact]
    public void Article_rejects_undefined_type()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Article.Create(Tenant, "A001", "Vijak", (ArticleType)99, Guid.NewGuid(), isActive: true));
    }

    [Fact]
    public void Article_requires_unit_of_measure()
    {
        Assert.Throws<ArgumentException>(() =>
            Article.Create(Tenant, "A001", "Vijak", ArticleType.Goods, Guid.Empty, isActive: true));
    }

    [Fact]
    public void Unit_of_measure_requires_code()
    {
        Assert.Throws<ArgumentException>(() => UnitOfMeasure.Create(Tenant, "", "Komad"));
    }

    [Fact]
    public void Entities_get_distinct_ids()
    {
        var first = UnitOfMeasure.Create(Tenant, "kom", "Komad");
        var second = UnitOfMeasure.Create(Tenant, "kom", "Komad");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(Tenant, first.CompanyId);
    }
}
