using System.Security.Claims;
using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Identity.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Modules.Identity.Infrastructure.Security;

/// <summary>
/// The access token only identifies the user; permissions are checked per request against
/// the database (see MemberPermissions), so it carries no roles.
/// </summary>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> jwt, IOptions<SessionOptions> sessions) : IAccessTokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken Issue(User user, DateTimeOffset now)
    {
        var expiresAt = now + sessions.Value.AccessTokenLifetime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Value.Issuer,
            Audience = jwt.Value.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimsPrincipalExtensions.UserIdClaim, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(jwt.Value.SecurityKey(), SecurityAlgorithms.HmacSha256),
        };
        return new AccessToken(Handler.CreateToken(descriptor), expiresAt);
    }
}
