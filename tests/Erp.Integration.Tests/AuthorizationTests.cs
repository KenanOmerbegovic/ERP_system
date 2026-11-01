using System.Net;
using System.Net.Http.Headers;
using Erp.Integration.Tests.TestSupport;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Roles;
using Erp.Modules.Identity.Contracts.Users;

namespace Erp.Integration.Tests;

/// <summary>Deny by default; permissions per company, checked against the database.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationTests(ApiFactory factory)
{
    [Fact]
    public async Task Endpoints_require_a_signed_in_user()
    {
        var response = await factory.CreateBrowserClient().GetAsync($"/api/companies/{Guid.NewGuid()}/articles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_forged_token_is_rejected()
    {
        var client = factory.CreateBrowserClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIwMTkwYTAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDAwMDEifQ.c2lnbmF0dXJl");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Members_cannot_reach_another_companys_data()
    {
        var (first, _, _) = await factory.RegisterCompanyAsync("4200000000201");
        var (_, secondCompanyId, _) = await factory.RegisterCompanyAsync("4200000000202");

        Assert.Equal(HttpStatusCode.Forbidden, (await first.GetAsync($"/api/companies/{secondCompanyId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await first.GetAsync($"/api/companies/{secondCompanyId}/articles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await first.GetAsync($"/api/companies/{secondCompanyId}/users")).StatusCode);
    }

    [Fact]
    public async Task Role_permissions_decide_what_a_member_may_do()
    {
        var (admin, companyId, _) = await factory.RegisterCompanyAsync("4200000000203");
        var role = await (await admin.PostJsonAsync(
            $"/api/companies/{companyId}/roles",
            new RoleRequest("Pregled artikala", [CorePermissions.ArticlesRead]))).ReadAsync<CreatedResponse>();
        var created = await admin.PostJsonAsync(
            $"/api/companies/{companyId}/users",
            new CreateUserRequest("pregled.4200000000203@firma.ba", "Pregled", ApiClient.Password, [role.Id]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var viewer = factory.CreateBrowserClient();
        viewer.SignIn(await viewer.LoginAsync("pregled.4200000000203@firma.ba"));

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/companies/{companyId}/articles")).StatusCode);
        var write = await viewer.PostJsonAsync(
            $"/api/companies/{companyId}/articles",
            new ArticleRequest("A1", "Artikal", ArticleKind.Goods, Guid.NewGuid(), true));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/users")).StatusCode);

        // Revoked on the next request, without waiting for the access token to expire.
        var update = await admin.PutAsync(
            $"/api/companies/{companyId}/roles/{role.Id}",
            System.Net.Http.Json.JsonContent.Create(new RoleRequest("Pregled artikala", []), options: ApiClient.Json));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/companies/{companyId}/articles")).StatusCode);
    }

    [Fact]
    public async Task Problem_responses_carry_the_error_code()
    {
        var (admin, companyId, adminEmail) = await factory.RegisterCompanyAsync("4200000000204");

        var duplicate = await admin.PostJsonAsync(
            $"/api/companies/{companyId}/users",
            new CreateUserRequest(adminEmail, "Isti", ApiClient.Password, []));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("User.DuplicateEmail", await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
