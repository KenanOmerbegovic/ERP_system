using Erp.Modules.Identity.Domain.Users;

namespace Erp.Modules.Identity.Application.Abstractions;

/// <summary>Creates signed, short-lived access tokens. Lifetime and keys come from configuration.</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(User user, DateTimeOffset now);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
