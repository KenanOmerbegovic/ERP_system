namespace Erp.Modules.Identity.Contracts.Auth;

/// <summary>
/// How the client receives and returns its refresh token. Browsers use <see cref="Cookie"/>
/// (httpOnly, never readable by script); desktop clients use <see cref="Body"/> and keep the
/// token in platform secure storage.
/// </summary>
public enum RefreshTokenDelivery
{
    Cookie,
    Body,
}

public sealed record LoginRequest(string Email, string Password, RefreshTokenDelivery TokenDelivery = RefreshTokenDelivery.Cookie);

/// <summary>With <see cref="RefreshTokenDelivery.Cookie"/> the token comes from the cookie and <see cref="RefreshToken"/> is ignored.</summary>
public sealed record RefreshRequest(string? RefreshToken = null, RefreshTokenDelivery TokenDelivery = RefreshTokenDelivery.Cookie);

/// <summary>The refresh token to revoke; when omitted, the one in the cookie.</summary>
public sealed record LogoutRequest(string? RefreshToken = null);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary><see cref="RefreshToken"/> is null when it was set as a cookie.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string? RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
