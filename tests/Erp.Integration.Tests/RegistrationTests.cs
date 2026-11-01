using System.Net;
using Erp.Integration.Tests.TestSupport;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Core.Contracts.Companies;
using Erp.Modules.Core.Contracts.UnitsOfMeasure;
using Erp.Modules.Identity;
using Erp.Modules.Identity.Contracts.Auth;
using Erp.Modules.Identity.Contracts.Registration;
using Erp.Modules.Identity.Contracts.Users;

namespace Erp.Integration.Tests;

[Collection(ApiCollection.Name)]
public sealed class RegistrationTests(ApiFactory factory)
{
    [Fact]
    public async Task Key_registers_account_then_company_and_makes_the_user_its_administrator()
    {
        var client = factory.CreateBrowserClient();
        var session = await (await client.PostJsonAsync(
            "/api/registration",
            new RegisterAccountRequest(await factory.IssueRegistrationKeyAsync(), "amra@bosna-trade.ba", "Amra Hadžić", ApiClient.Password, RefreshTokenDelivery.Body)))
            .ReadAsync<AuthResponse>();
        client.SignIn(session);
        var before = await (await client.GetAsync("/api/me")).ReadAsync<CurrentUserResponse>();
        Assert.True(before.CanRegisterCompany);
        Assert.Empty(before.Companies);

        var created = await client.PostJsonAsync(
            "/api/companies",
            new CompanyRequest("Bosna Trade d.o.o.", "4200000000101", null, "Ferhadija 1", "71000", "Sarajevo"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var companyId = (await created.ReadAsync<Modules.Core.Contracts.Common.CreatedResponse>()).Id;
        var me = await ApiClient.WaitUntilMemberAsync(client, companyId);
        var membership = Assert.Single(me.Companies);
        Assert.Equal("Bosna Trade d.o.o.", membership.Name);
        Assert.True(membership.IsAdministrator);
        Assert.Contains(CorePermissions.ArticlesWrite, membership.Permissions);
        Assert.False(me.CanRegisterCompany);

        var company = await (await client.GetAsync($"/api/companies/{companyId}")).ReadAsync<CompanyResponse>();
        Assert.Equal("4200000000101", company.TaxId);
        var unit = await client.PostJsonAsync($"/api/companies/{companyId}/units-of-measure", new UnitOfMeasureRequest("kom", "Komad"));
        Assert.Equal(HttpStatusCode.Created, unit.StatusCode);
    }

    [Fact]
    public async Task A_key_creates_only_one_company()
    {
        var (client, _, _) = await factory.RegisterCompanyAsync("4200000000102");

        var second = await client.PostJsonAsync(
            "/api/companies",
            new CompanyRequest("Druga firma d.o.o.", "4200000000103", null, "Titova 5", "71000", "Sarajevo"));

        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
    }

    [Fact]
    public async Task Creating_a_company_needs_a_session_from_a_key()
    {
        var anonymous = await factory.CreateBrowserClient().PostJsonAsync(
            "/api/companies",
            new CompanyRequest("Firma d.o.o.", "4200000000104", null, "Titova 5", "71000", "Sarajevo"));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Invalid_key_is_rejected_with_its_code()
    {
        var response = await factory.CreateBrowserClient().PostJsonAsync(
            "/api/registration",
            new RegisterAccountRequest("AAAAA-BBBBB-CCCCC-DDDDD-EEEEE", "x@firma.ba", "X", ApiClient.Password));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Registration.InvalidKey", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Command_line_issues_a_key_that_registers()
    {
        using var output = new StringWriter();

        var handled = await factory.Services.TryRunCommandAsync(["issue-registration-key", "--days", "3"], output, CancellationToken.None);

        Assert.True(handled);
        var key = output.ToString().Split(Environment.NewLine)[0]["Registration key: ".Length..];
        var response = await factory.CreateBrowserClient().PostJsonAsync(
            "/api/registration",
            new RegisterAccountRequest(key, "cli@firma.ba", "CLI", ApiClient.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Other_arguments_are_not_commands()
    {
        Assert.False(await factory.Services.TryRunCommandAsync([], TextWriter.Null, CancellationToken.None));
        Assert.False(await factory.Services.TryRunCommandAsync(["--urls", "http://x"], TextWriter.Null, CancellationToken.None));
    }
}
