namespace Erp.Modules.Identity.Domain.Sessions;

/// <summary>
/// One link in a rotating chain of refresh tokens (a family, started by a login). Each refresh
/// revokes the presented token and issues the next one; presenting a revoked token means it
/// was stolen or replayed, and the whole family is revoked. Only the hash is stored.
/// Platform-level like <see cref="Users.User"/>, so no CompanyId.
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken(Guid id, Guid userId, Guid familyId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    private RefreshToken()
    {
    }

    public Guid Id { get; private init; }

    public Guid UserId { get; private init; }

    public Guid FamilyId { get; private init; }

    public string TokenHash { get; private init; } = null!;

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Starts a new family, at login.</summary>
    public static RefreshToken Start(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), userId, Guid.CreateVersion7(), tokenHash, now, now + lifetime);

    /// <summary>The next token in this family; the caller revokes this one.</summary>
    public RefreshToken Rotate(string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), UserId, FamilyId, tokenHash, now, now + lifetime);

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && ExpiresAt > now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
