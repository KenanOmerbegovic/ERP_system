using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Contracts.Companies;
using Erp.Modules.Identity.Contracts.Auth;
using Erp.Modules.Identity.Contracts.Registration;
using Erp.Modules.Identity.Contracts.Users;

namespace Erp.Integration.Tests.TestSupport;

/// <summary>Calls the API the way the client app will, with the contract DTOs.</summary>
internal static class ApiClient
{
    public const string Password = "correct horse battery";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static void SignIn(this HttpClient client, AuthResponse session) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

    /// <summary>Both registration steps; returns the administrator's signed-in client and the company id.</summary>
    public static async Task<(HttpClient Client, Guid CompanyId, string Email)> RegisterCompanyAsync(this ApiFactory factory, string taxId)
    {
        var client = factory.CreateBrowserClient();
        var email = $"admin.{taxId}@firma.ba";
        var session = await (await client.PostJsonAsync(
            "/api/registration",
            new RegisterAccountRequest(await factory.IssueRegistrationKeyAsync(), email, "Administrator", Password, RefreshTokenDelivery.Body))).ReadAsync<AuthResponse>();
        client.SignIn(session);

        var created = await (await client.PostJsonAsync(
            "/api/companies",
            new CompanyRequest($"Firma {taxId} d.o.o.", taxId, null, "Ferhadija 1", "71000", "Sarajevo"))).ReadAsync<CreatedResponse>();

        await WaitUntilMemberAsync(client, created.Id);
        return (client, created.Id, email);
    }

    /// <summary>Membership arrives through Core's outbox moments after the company is created.</summary>
    public static async Task<CurrentUserResponse> WaitUntilMemberAsync(HttpClient client, Guid companyId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            var me = await (await client.GetAsync("/api/me")).ReadAsync<CurrentUserResponse>();
            if (me.Companies.Any(company => company.CompanyId == companyId))
            {
                return me;
            }

            Assert.True(DateTime.UtcNow < deadline, "The registering user did not become a member of the new company.");
            await Task.Delay(100);
        }
    }

    public static async Task<AuthResponse> LoginAsync(this HttpClient client, string email, RefreshTokenDelivery delivery = RefreshTokenDelivery.Body)
    {
        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, Password, delivery));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<AuthResponse>();
    }
}
