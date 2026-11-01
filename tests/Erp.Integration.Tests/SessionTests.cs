using System.Net;
using Erp.Integration.Tests.TestSupport;
using Erp.Modules.Identity.Contracts.Auth;

namespace Erp.Integration.Tests;

/// <summary>Refresh tokens: an httpOnly cookie for browsers, the response body for desktop clients.</summary>
[Collection(ApiCollection.Name)]
public sealed class SessionTests(ApiFactory factory)
{
    [Fact]
    public async Task Browser_login_sets_a_locked_down_cookie_and_hides_the_token()
    {
        var (_, _, email) = await factory.RegisterCompanyAsync("4200000000301");
        var browser = factory.CreateBrowserClient();

        var response = await browser.PostJsonAsync("/api/auth/login", new LoginRequest(email, ApiClient.Password));

        var session = await response.ReadAsync<AuthResponse>();
        Assert.Null(session.RefreshToken);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("erp_refresh=", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Browser_refreshes_with_the_cookie_and_logout_ends_it()
    {
        var (_, _, email) = await factory.RegisterCompanyAsync("4200000000302");
        var browser = factory.CreateBrowserClient();
        await browser.LoginAsync(email, RefreshTokenDelivery.Cookie);

        var refreshed = await browser.PostJsonAsync("/api/auth/refresh", new RefreshRequest());
        var session = await refreshed.ReadAsync<AuthResponse>();
        Assert.Null(session.RefreshToken);
        browser.SignIn(session);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostJsonAsync("/api/auth/logout", new LogoutRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.PostJsonAsync("/api/auth/refresh", new RefreshRequest())).StatusCode);
    }

    [Fact]
    public async Task Desktop_gets_the_token_in_the_body_and_a_replayed_token_ends_the_session()
    {
        var (_, _, email) = await factory.RegisterCompanyAsync("4200000000303");
        var desktop = factory.CreateClient();
        var login = await desktop.LoginAsync(email, RefreshTokenDelivery.Body);
        Assert.NotNull(login.RefreshToken);

        var rotated = await (await desktop.PostJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken, RefreshTokenDelivery.Body)))
            .ReadAsync<AuthResponse>();
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);

        var replay = await desktop.PostJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken, RefreshTokenDelivery.Body));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var afterReplay = await desktop.PostJsonAsync("/api/auth/refresh", new RefreshRequest(rotated.RefreshToken, RefreshTokenDelivery.Body));
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_unauthorized_with_its_code()
    {
        var (_, _, email) = await factory.RegisterCompanyAsync("4200000000304");

        var response = await factory.CreateClient().PostJsonAsync("/api/auth/login", new LoginRequest(email, "not the password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Auth.InvalidCredentials", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_is_rate_limited()
    {
        using var strict = factory.WithSettings(("Identity:RateLimiting:PermitLimit", "3"));
        var client = strict.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            statuses.Add((await client.PostJsonAsync("/api/auth/login", new LoginRequest("nobody@firma.ba", "whatever password"))).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Cors_allows_the_configured_origin_with_credentials_only()
    {
        var client = factory.CreateClient();

        var allowed = await client.SendAsync(Preflight(ApiFactory.AllowedOrigin));
        var other = await client.SendAsync(Preflight("https://evil.test"));

        Assert.Equal(ApiFactory.AllowedOrigin, Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.False(other.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/refresh");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return request;
    }
}
