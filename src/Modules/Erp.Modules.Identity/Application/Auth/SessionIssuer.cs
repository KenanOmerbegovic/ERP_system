using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Domain.Sessions;
using Erp.Modules.Identity.Domain.Users;
using Microsoft.Extensions.Options;

namespace Erp.Modules.Identity.Application.Auth;

/// <summary>A signed-in session: the access token and the refresh token that renews it.</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Issues tokens for login and refresh. Adds the new refresh token to the context; the caller saves.</summary>
public sealed class SessionIssuer(IIdentityDbContext db, IAccessTokenIssuer accessTokens, IOptions<SessionOptions> options)
{
    public AuthTokens Start(User user, DateTimeOffset now)
    {
        var refreshToken = Secrets.NewRefreshToken();
        var stored = RefreshToken.Start(user.Id, Secrets.Hash(refreshToken), now, options.Value.RefreshTokenLifetime);
        return Issue(user, stored, refreshToken, now);
    }

    public AuthTokens Rotate(User user, RefreshToken current, DateTimeOffset now)
    {
        var refreshToken = Secrets.NewRefreshToken();
        current.Revoke(now);
        var stored = current.Rotate(Secrets.Hash(refreshToken), now, options.Value.RefreshTokenLifetime);
        return Issue(user, stored, refreshToken, now);
    }

    private AuthTokens Issue(User user, RefreshToken stored, string refreshToken, DateTimeOffset now)
    {
        db.RefreshTokens.Add(stored);
        var accessToken = accessTokens.Issue(user, now);
        return new AuthTokens(accessToken.Value, accessToken.ExpiresAt, refreshToken, stored.ExpiresAt);
    }
}
