using Erp.Modules.Identity.Application.Auth;
using Erp.Modules.Identity.Contracts.Auth;
using Microsoft.AspNetCore.Http;

namespace Erp.Modules.Identity.Endpoints;

/// <summary>
/// The browser's refresh token: httpOnly so script can't read it, Secure, SameSite=Strict
/// so other sites can't make the browser send it, and sent only to the auth endpoints.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "erp_refresh";
    public const string Path = "/api/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, Options(expires: null));

    /// <summary>Sets the cookie or puts the token in the body, as the client asked.</summary>
    public static AuthResponse Respond(HttpResponse response, AuthTokens tokens, RefreshTokenDelivery delivery)
    {
        if (delivery == RefreshTokenDelivery.Body)
        {
            return new AuthResponse(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        }

        response.Cookies.Append(Name, tokens.RefreshToken, Options(tokens.RefreshTokenExpiresAt));
        return new AuthResponse(tokens.AccessToken, tokens.AccessTokenExpiresAt, null, tokens.RefreshTokenExpiresAt);
    }

    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
        IsEssential = true,
    };
}
