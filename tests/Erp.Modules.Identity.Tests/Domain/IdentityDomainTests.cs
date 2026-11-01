using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Domain.Registration;
using Erp.Modules.Identity.Domain.Roles;
using Erp.Modules.Identity.Domain.Sessions;
using Erp.Modules.Identity.Domain.Users;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Identity.Tests.Domain;

public class IdentityDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Lockout_starts_at_the_limit_and_resets_the_count()
    {
        var user = User.Create("a@b.ba", "A", "hash", Now);

        user.RecordFailedLogin(Now, maxFailedAttempts: 2, TimeSpan.FromMinutes(5));
        Assert.False(user.IsLockedOut(Now));
        user.RecordFailedLogin(Now, maxFailedAttempts: 2, TimeSpan.FromMinutes(5));

        Assert.True(user.IsLockedOut(Now.AddMinutes(4)));
        Assert.False(user.IsLockedOut(Now.AddMinutes(5)));
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void Rotated_refresh_token_stays_in_its_family()
    {
        var first = RefreshToken.Start(Guid.NewGuid(), "h1", Now, TimeSpan.FromDays(1));

        var next = first.Rotate("h2", Now.AddHours(1), TimeSpan.FromDays(1));

        Assert.Equal(first.FamilyId, next.FamilyId);
        Assert.Equal(first.UserId, next.UserId);
        Assert.Equal(Now.AddHours(1).AddDays(1), next.ExpiresAt);
    }

    [Fact]
    public void Revoking_twice_keeps_the_first_time()
    {
        var token = RefreshToken.Start(Guid.NewGuid(), "h", Now, TimeSpan.FromDays(1));

        token.Revoke(Now);
        token.Revoke(Now.AddHours(1));

        Assert.Equal(Now, token.RevokedAt);
        Assert.False(token.IsActive(Now));
    }

    [Fact]
    public void Registration_key_goes_issued_redeemed_used()
    {
        var key = RegistrationKey.Issue("h", Now, TimeSpan.FromDays(7));
        var userId = Guid.NewGuid();
        var companyId = CompanyId.New();

        Assert.True(key.CanBeRedeemed(Now));
        key.Redeem(userId, Now);
        Assert.False(key.CanBeRedeemed(Now));
        key.UseFor(companyId);

        Assert.Equal(userId, key.RedeemedByUserId);
        Assert.Equal(companyId, key.UsedForCompanyId);
        Assert.Throws<InvalidOperationException>(() => key.UseFor(CompanyId.New()));
    }

    [Fact]
    public void Registration_key_cannot_be_redeemed_after_expiry_or_used_before_redeemed()
    {
        var key = RegistrationKey.Issue("h", Now, TimeSpan.FromDays(7));

        Assert.Throws<InvalidOperationException>(() => key.Redeem(Guid.NewGuid(), Now.AddDays(7)));
        Assert.Throws<InvalidOperationException>(() => key.UseFor(CompanyId.New()));
    }

    [Fact]
    public void Administrator_role_grants_everything_and_cannot_change()
    {
        var role = Role.CreateAdministrator(CompanyId.New());

        Assert.True(role.Grants("any.permission.at.all"));
        Assert.Throws<InvalidOperationException>(() => role.Update("Other", []));
    }

    [Fact]
    public void Registration_keys_are_random_and_normalize_loosely()
    {
        var key = Secrets.NewRegistrationKey();

        Assert.NotEqual(key, Secrets.NewRegistrationKey());
        Assert.Equal(Secrets.NormalizeRegistrationKey(key), Secrets.NormalizeRegistrationKey(" " + key.ToLowerInvariant().Replace('-', ' ') + " "));
        Assert.Equal(25, Secrets.NormalizeRegistrationKey(key).Length);
    }

    [Fact]
    public void Hash_is_hex_sha256()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Secrets.Hash("abc"));
    }
}
