using Erp.Modules.Core.Application;
using Erp.Modules.Core.Application.BusinessPartners;
using Erp.Modules.Core.Tests.TestSupport;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Tests.Application;

public sealed class BusinessPartnerTests : IDisposable
{
    private readonly CoreTestDatabase _database = new();
    private readonly CoreHandlers _handlers;

    public BusinessPartnerTests()
    {
        _handlers = new CoreHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Create_saves_partner_in_company()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, code: " K001 ", isCustomer: true, isSupplier: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var partner = await _database.Query().BusinessPartners.SingleAsync();
        Assert.Equal(result.Value, partner.Id);
        Assert.Equal(companyId, partner.CompanyId);
        Assert.Equal("K001", partner.Code);
        Assert.True(partner.IsCustomer);
        Assert.True(partner.IsSupplier);
        Assert.True(partner.IsActive);
    }

    [Fact]
    public async Task Create_without_country_uses_domestic_country()
    {
        var companyId = await _handlers.ACompanyAsync();

        await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, countryCode: null),
            CancellationToken.None);

        var partner = await _database.Query().BusinessPartners.SingleAsync();
        Assert.Equal(FakeTaxIdentifierRules.Domestic, partner.CountryCode);
    }

    [Fact]
    public async Task Create_normalizes_country_code_to_upper_case()
    {
        var companyId = await _handlers.ACompanyAsync();

        await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, countryCode: "hr", taxId: "12345678901"),
            CancellationToken.None);

        var partner = await _database.Query().BusinessPartners.SingleAsync();
        Assert.Equal("HR", partner.CountryCode);
    }

    [Fact]
    public async Task Create_checks_domestic_tax_ids_against_country_rules()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, taxId: FakeTaxIdentifierRules.Invalid, vatNumber: FakeTaxIdentifierRules.Invalid),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "TaxId", "TaxId.Invalid");
        ResultAssert.FailsValidation(result, "VatNumber", "VatNumber.Invalid");
    }

    [Fact]
    public async Task Create_does_not_apply_domestic_rules_to_foreign_partners()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, countryCode: "DE", taxId: FakeTaxIdentifierRules.Invalid),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_allows_partner_without_tax_id()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, taxId: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null((await _database.Query().BusinessPartners.SingleAsync()).TaxId);
    }

    [Theory]
    [InlineData("BIH")]
    [InlineData("B")]
    [InlineData("1A")]
    public async Task Create_rejects_country_code_that_is_not_two_letters(string countryCode)
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, countryCode: countryCode),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "CountryCode", "CountryCode.Invalid");
    }

    [Fact]
    public async Task Create_requires_code_and_name()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, code: "", name: ""),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "Code", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "Name", "NotEmptyValidator");
    }

    [Fact]
    public async Task Create_rejects_code_longer_than_limit()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, code: new string('K', 51)),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "Code", "MaximumLengthValidator");
    }

    [Fact]
    public async Task Create_in_unknown_company_is_not_found()
    {
        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(CompanyId.New()),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.CompanyNotFound);
    }

    [Fact]
    public async Task Create_rejects_duplicate_code_in_same_company()
    {
        var companyId = await _handlers.ACompanyAsync();
        await _handlers.APartnerAsync(companyId, code: "K001");

        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(companyId, code: "K001", name: "Drugi kupac"),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.DuplicateBusinessPartnerCode);
    }

    [Fact]
    public async Task Same_code_is_allowed_in_different_companies()
    {
        var first = await _handlers.ACompanyAsync(taxId: "4200000000001");
        var second = await _handlers.ACompanyAsync(taxId: "4200000000002");
        await _handlers.APartnerAsync(first, code: "K001");

        var result = await _handlers.CreateBusinessPartner.HandleAsync(
            Commands.CreateBusinessPartner(second, code: "K001"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Update_changes_fields()
    {
        var companyId = await _handlers.ACompanyAsync();
        var partnerId = await _handlers.APartnerAsync(companyId);

        var result = await _handlers.UpdateBusinessPartner.HandleAsync(
            Commands.UpdateBusinessPartner(companyId, partnerId, code: "K002", name: "Preimenovan d.o.o.", isActive: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var partner = await _database.Query().BusinessPartners.SingleAsync();
        Assert.Equal("K002", partner.Code);
        Assert.Equal("Preimenovan d.o.o.", partner.Name);
        Assert.False(partner.IsActive);
    }

    [Fact]
    public async Task Update_cannot_reach_partner_of_another_company()
    {
        var owner = await _handlers.ACompanyAsync(taxId: "4200000000001");
        var other = await _handlers.ACompanyAsync(taxId: "4200000000002");
        var partnerId = await _handlers.APartnerAsync(owner);

        var result = await _handlers.UpdateBusinessPartner.HandleAsync(
            Commands.UpdateBusinessPartner(other, partnerId),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.BusinessPartnerNotFound);
    }

    [Fact]
    public async Task Update_rejects_code_of_another_partner()
    {
        var companyId = await _handlers.ACompanyAsync();
        await _handlers.APartnerAsync(companyId, code: "K001");
        var secondId = await _handlers.APartnerAsync(companyId, code: "K002");

        var result = await _handlers.UpdateBusinessPartner.HandleAsync(
            Commands.UpdateBusinessPartner(companyId, secondId, code: "K001"),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.DuplicateBusinessPartnerCode);
    }

    [Fact]
    public async Task Update_validates_input()
    {
        var companyId = await _handlers.ACompanyAsync();
        var partnerId = await _handlers.APartnerAsync(companyId);

        var result = await _handlers.UpdateBusinessPartner.HandleAsync(
            Commands.UpdateBusinessPartner(companyId, partnerId, taxId: FakeTaxIdentifierRules.Invalid),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "TaxId", "TaxId.Invalid");
    }

    [Fact]
    public async Task List_returns_only_partners_of_the_company_ordered_by_code()
    {
        var companyId = await _handlers.ACompanyAsync(taxId: "4200000000001");
        var otherId = await _handlers.ACompanyAsync(taxId: "4200000000002");
        await _handlers.APartnerAsync(companyId, code: "K002");
        await _handlers.APartnerAsync(companyId, code: "K001");
        await _handlers.APartnerAsync(otherId, code: "K000");

        var result = await _handlers.ListBusinessPartners.HandleAsync(
            new ListBusinessPartners(companyId, null, 1, 50),
            CancellationToken.None);

        Assert.Equal(["K001", "K002"], result.Value.Items.Select(p => p.Code));
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task List_searches_code_and_name_case_insensitively()
    {
        var companyId = await _handlers.ACompanyAsync();
        await _handlers.APartnerAsync(companyId, code: "K001", name: "Bosna Trade d.o.o.");
        await _handlers.APartnerAsync(companyId, code: "D001", name: "Dobavljač d.o.o.");
        await _handlers.APartnerAsync(companyId, code: "X-BOS", name: "Treći");

        var result = await _handlers.ListBusinessPartners.HandleAsync(
            new ListBusinessPartners(companyId, " bos ", 1, 50),
            CancellationToken.None);

        Assert.Equal(["K001", "X-BOS"], result.Value.Items.Select(p => p.Code));
    }

    [Fact]
    public async Task List_pages_results()
    {
        var companyId = await _handlers.ACompanyAsync();
        foreach (var code in new[] { "K1", "K2", "K3", "K4", "K5" })
        {
            await _handlers.APartnerAsync(companyId, code: code);
        }

        var result = await _handlers.ListBusinessPartners.HandleAsync(
            new ListBusinessPartners(companyId, null, Page: 2, PageSize: 2),
            CancellationToken.None);

        Assert.Equal(["K3", "K4"], result.Value.Items.Select(p => p.Code));
        Assert.Equal(5, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(2, result.Value.PageSize);
    }

    [Theory]
    [InlineData(0, 50, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    public async Task List_validates_paging(int page, int pageSize, string property)
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.ListBusinessPartners.HandleAsync(
            new ListBusinessPartners(companyId, null, page, pageSize),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(property, Assert.IsType<SharedKernel.Results.ValidationError>(result.Error).Errors.Keys);
    }

    [Fact]
    public async Task List_of_unknown_company_is_not_found()
    {
        var result = await _handlers.ListBusinessPartners.HandleAsync(
            new ListBusinessPartners(CompanyId.New(), null, 1, 50),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.CompanyNotFound);
    }
}
