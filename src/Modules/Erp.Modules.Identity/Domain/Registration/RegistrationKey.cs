using Erp.SharedKernel.Domain;

namespace Erp.Modules.Identity.Domain.Registration;

/// <summary>
/// A single-use key the operator issues so one company can register itself. Issued, then
/// redeemed by the account it creates, then used when that account creates the company.
/// Only the hash is stored. Platform-level, so no CompanyId of its own.
/// </summary>
public sealed class RegistrationKey
{
    private RegistrationKey(Guid id, string keyHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        KeyHash = keyHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    private RegistrationKey()
    {
    }

    public Guid Id { get; private init; }

    public string KeyHash { get; private init; } = null!;

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Applies to redeeming only; a redeemed key stays usable until its company is created.</summary>
    public DateTimeOffset ExpiresAt { get; private init; }

    public Guid? RedeemedByUserId { get; private set; }

    public DateTimeOffset? RedeemedAt { get; private set; }

    /// <summary>The company created with this key; set once, after which the key is spent.</summary>
    public CompanyId? UsedForCompanyId { get; private set; }

    public static RegistrationKey Issue(string keyHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), keyHash, now, now + lifetime);

    public bool CanBeRedeemed(DateTimeOffset now) => RedeemedByUserId is null && ExpiresAt > now;

    public void Redeem(Guid userId, DateTimeOffset now)
    {
        if (!CanBeRedeemed(now))
        {
            throw new InvalidOperationException("The registration key cannot be redeemed.");
        }

        RedeemedByUserId = userId;
        RedeemedAt = now;
    }

    public void UseFor(CompanyId companyId)
    {
        if (RedeemedByUserId is null || UsedForCompanyId is not null)
        {
            throw new InvalidOperationException("Only a redeemed, unused registration key can be used.");
        }

        UsedForCompanyId = companyId;
    }
}
