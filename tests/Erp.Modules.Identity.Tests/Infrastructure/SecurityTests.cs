using Erp.Modules.Identity.Domain.Users;
using Erp.Modules.Identity.Infrastructure.Security;
using Erp.Modules.Identity.Tests.TestSupport;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Modules.Identity.Tests.Infrastructure;

public class SecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Password_hash_verifies_only_the_same_password()
    {
        var hasher = new PasswordHasher();

        var hash = hasher.Hash("correct horse battery");

        Assert.NotEqual("correct horse battery", hash);
        Assert.True(hasher.Verify(hash, "correct horse battery"));
        Assert.False(hasher.Verify(hash, "Correct horse battery"));
        Assert.NotEqual(hash, hasher.Hash("correct horse battery"));
    }

    [Fact]
    public async Task Access_token_is_signed_and_identifies_the_user()
    {
        var user = User.Create("emir@firma.ba", "Emir Kovač", "hash", Now);
        var issuer = new IdentityHandlers(null!).AccessTokens;

        var token = issuer.Issue(user, Now);

        Assert.Equal(Now + IdentityHandlers.Sessions.AccessTokenLifetime, token.ExpiresAt);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = IdentityHandlers.Jwt.Issuer,
            ValidAudience = IdentityHandlers.Jwt.Audience,
            IssuerSigningKey = IdentityHandlers.Jwt.SecurityKey(),
            ValidateLifetime = false,
        });
        Assert.True(validation.IsValid, validation.Exception?.Message);
        Assert.Equal(user.Id.ToString(), validation.Claims["sub"]);
        Assert.Equal("emir@firma.ba", validation.Claims["email"]);
    }

    [Fact]
    public async Task Access_token_with_another_key_is_rejected()
    {
        var token = new IdentityHandlers(null!).AccessTokens.Issue(User.Create("a@b.ba", "A", "hash", Now), Now);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = IdentityHandlers.Jwt.Issuer,
            ValidAudience = IdentityHandlers.Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(new byte[32]),
            ValidateLifetime = false,
        });

        Assert.False(validation.IsValid);
    }

    [Theory]
    [InlineData("short", false)]
    [InlineData("exactly-thirty-two-bytes-long-ok", true)]
    public void Signing_key_needs_256_bits(string key, bool valid)
    {
        Assert.Equal(valid, JwtOptions.HasValidSigningKey(new JwtOptions { Issuer = "i", Audience = "a", SigningKey = key }));
    }
}
