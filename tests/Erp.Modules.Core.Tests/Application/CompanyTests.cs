using System.Text.Json;
using Erp.Messaging.Outbox;
using Erp.Modules.Core.Application;
using Erp.Modules.Core.Application.Companies;
using Erp.Modules.Core.Contracts.Companies;
using Erp.Modules.Core.Tests.TestSupport;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Tests.Application;

public sealed class CompanyTests : IDisposable
{
    private readonly CoreTestDatabase _database = new();
    private readonly CoreHandlers _handlers;

    public CompanyTests()
    {
        _handlers = new CoreHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Create_saves_company_with_trimmed_values()
    {
        var result = await _handlers.CreateCompany.HandleAsync(
            Commands.CreateCompany(name: "  Firma d.o.o. ", taxId: " 4200000000001 ", city: " Mostar "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var company = await _database.Query().Companies.SingleAsync();
        Assert.Equal(result.Value, company.Id);
        Assert.Equal(company.Id, company.CompanyId);
        Assert.Equal("Firma d.o.o.", company.Name);
        Assert.Equal("4200000000001", company.TaxId);
        Assert.Equal("Mostar", company.City);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_without_vat_number_means_not_vat_registered(string? vatNumber)
    {
        await _handlers.CreateCompany.HandleAsync(Commands.CreateCompany(vatNumber: vatNumber), CancellationToken.None);

        var company = await _database.Query().Companies.SingleAsync();
        Assert.Null(company.VatNumber);
        Assert.False(company.IsVatRegistered);
    }

    [Fact]
    public async Task Create_requires_name()
    {
        var result = await _handlers.CreateCompany.HandleAsync(Commands.CreateCompany(name: " "), CancellationToken.None);

        ResultAssert.FailsValidation(result, "Name", "NotEmptyValidator");
        Assert.Empty(_database.Query().Companies);
    }

    [Fact]
    public async Task Create_rejects_tax_id_that_breaks_country_rules()
    {
        var result = await _handlers.CreateCompany.HandleAsync(
            Commands.CreateCompany(taxId: FakeTaxIdentifierRules.Invalid),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "TaxId", "TaxId.Invalid");
    }

    [Fact]
    public async Task Create_rejects_vat_number_that_breaks_country_rules()
    {
        var result = await _handlers.CreateCompany.HandleAsync(
            Commands.CreateCompany(vatNumber: FakeTaxIdentifierRules.Invalid),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "VatNumber", "VatNumber.Invalid");
    }

    [Fact]
    public async Task Create_requires_address_postal_code_and_city()
    {
        var result = await _handlers.CreateCompany.HandleAsync(
            Commands.CreateCompany(address: "", postalCode: "", city: ""),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "Address", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "PostalCode", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "City", "NotEmptyValidator");
    }

    [Fact]
    public async Task Create_rejects_duplicate_tax_id()
    {
        await _handlers.ACompanyAsync(taxId: "4200000000001");

        var result = await _handlers.CreateCompany.HandleAsync(
            Commands.CreateCompany(name: "Druga firma", taxId: "4200000000001"),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.DuplicateCompanyTaxId);
    }

    [Fact]
    public async Task Update_changes_fields()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.UpdateCompany.HandleAsync(
            Commands.UpdateCompany(companyId, name: "Novo ime d.o.o.", vatNumber: null, city: "Tuzla"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var company = await _database.Query().Companies.SingleAsync();
        Assert.Equal("Novo ime d.o.o.", company.Name);
        Assert.Null(company.VatNumber);
        Assert.Equal("Tuzla", company.City);
    }

    [Fact]
    public async Task Update_can_keep_own_tax_id()
    {
        var companyId = await _handlers.ACompanyAsync(taxId: "4200000000001");

        var result = await _handlers.UpdateCompany.HandleAsync(
            Commands.UpdateCompany(companyId, taxId: "4200000000001"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Update_rejects_tax_id_of_another_company()
    {
        await _handlers.ACompanyAsync(taxId: "4200000000001");
        var secondId = await _handlers.ACompanyAsync(taxId: "4200000000002");

        var result = await _handlers.UpdateCompany.HandleAsync(
            Commands.UpdateCompany(secondId, taxId: "4200000000001"),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.DuplicateCompanyTaxId);
    }

    [Fact]
    public async Task Update_unknown_company_is_not_found()
    {
        var result = await _handlers.UpdateCompany.HandleAsync(
            Commands.UpdateCompany(CompanyId.New()),
            CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.CompanyNotFound);
    }

    [Fact]
    public async Task Update_validates_input()
    {
        var companyId = await _handlers.ACompanyAsync();

        var result = await _handlers.UpdateCompany.HandleAsync(
            Commands.UpdateCompany(companyId, taxId: ""),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "TaxId", "NotEmptyValidator");
    }

    [Fact]
    public async Task Create_publishes_company_created_for_the_registering_user()
    {
        var result = await _handlers.CreateCompany.HandleAsync(Commands.CreateCompany(name: "Firma d.o.o."), CancellationToken.None);

        var created = Assert.IsType<CompanyCreated>(Assert.Single(await OutboxEventsAsync()));
        Assert.Equal(result.Value.Value, created.CompanyId);
        Assert.Equal("Firma d.o.o.", created.Name);
        Assert.Equal(Commands.RegisteringUserId, created.CreatedByUserId);
        Assert.Equal(_handlers.Clock.UtcNow, created.OccurredAt);
    }

    [Fact]
    public async Task Failed_create_publishes_nothing()
    {
        await _handlers.CreateCompany.HandleAsync(Commands.CreateCompany(name: ""), CancellationToken.None);

        Assert.Empty(await OutboxEventsAsync());
    }

    [Fact]
    public async Task Update_publishes_company_updated_with_new_name()
    {
        var companyId = await _handlers.ACompanyAsync();

        await _handlers.UpdateCompany.HandleAsync(Commands.UpdateCompany(companyId, name: "Novo ime d.o.o."), CancellationToken.None);

        var updated = Assert.Single((await OutboxEventsAsync()).OfType<CompanyUpdated>());
        Assert.Equal(companyId.Value, updated.CompanyId);
        Assert.Equal("Novo ime d.o.o.", updated.Name);
    }

    [Fact]
    public async Task Get_returns_company()
    {
        var companyId = await _handlers.ACompanyAsync(name: "Bosna Trade d.o.o.");

        var result = await _handlers.GetCompany.HandleAsync(new GetCompany(companyId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(companyId.Value, result.Value.Id);
        Assert.Equal("Bosna Trade d.o.o.", result.Value.Name);
    }

    [Fact]
    public async Task Get_unknown_company_is_not_found()
    {
        var result = await _handlers.GetCompany.HandleAsync(new GetCompany(CompanyId.New()), CancellationToken.None);

        ResultAssert.Fails(result, CoreErrors.CompanyNotFound);
    }

    private async Task<IReadOnlyList<object>> OutboxEventsAsync() =>
        (await _database.Query().Set<OutboxMessage>().OrderBy(message => message.OccurredAt).ToListAsync())
            .Select(message => JsonSerializer.Deserialize(message.Content, typeof(CompanyCreated).Assembly.GetType(message.Type)!, JsonSerializerOptions.Web)!)
            .ToList();
}
