namespace Erp.Modules.Identity.Domain.Users;

/// <summary>
/// A login. Users are platform-level, not company data, so they have no CompanyId:
/// a user reaches a company's data only through a <see cref="Memberships.Membership"/>.
/// </summary>
public sealed class User
{
    private User(Guid id, string email, string displayName, string passwordHash, DateTimeOffset createdAt)
    {
        Id = id;
        Email = NormalizeEmail(email);
        DisplayName = Text.Required(displayName);
        PasswordHash = Text.Required(passwordHash);
        CreatedAt = createdAt;
    }

    private User()
    {
    }

    public Guid Id { get; private init; }

    /// <summary>Normalized (trimmed, lower case); unique across all companies.</summary>
    public string Email { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Failed logins since the last successful one or the last lockout.</summary>
    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public static string NormalizeEmail(string email) => Text.Required(email).ToLowerInvariant();

    public static User Create(string email, string displayName, string passwordHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), email, displayName, passwordHash, now);

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil > now;

    /// <summary>Locks the account for <paramref name="lockoutDuration"/> once <paramref name="maxFailedAttempts"/> is reached.</summary>
    public void RecordFailedLogin(DateTimeOffset now, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxFailedAttempts)
        {
            LockedUntil = now + lockoutDuration;
            FailedLoginCount = 0;
        }
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    public void ChangePasswordHash(string passwordHash) => PasswordHash = Text.Required(passwordHash);
}
