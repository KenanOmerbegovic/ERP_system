using Erp.Modules.Identity.Application;
using Erp.Modules.Identity.Application.Auth;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Tests.Application;

/// <summary>Refresh-token rotation, reuse detection, logout and password change.</summary>
public sealed class SessionTests : IDisposable
{
    private readonly IdentityTestDatabase _database = new();
    private readonly IdentityHandlers _handlers;

    public SessionTests()
    {
        _handlers = new IdentityHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var (_, tokens) = await _handlers.ARegisteredAccountAsync();
        _handlers.Clock.Advance(TimeSpan.FromMinutes(10));

        var result = await _handlers.Refresh.HandleAsync(new RefreshSession(tokens.RefreshToken), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(tokens.RefreshToken, result.Value.RefreshToken);
        Assert.Equal(_handlers.Clock.UtcNow + IdentityHandlers.Sessions.RefreshTokenLifetime, result.Value.RefreshTokenExpiresAt);
        var stored = await _database.Query().RefreshTokens.ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.Single(stored.Select(t => t.FamilyId).Distinct());
        Assert.NotNull(stored.Single(t => t.TokenHash == Secrets.Hash(tokens.RefreshToken)).RevokedAt);
        Assert.Null(stored.Single(t => t.TokenHash == Secrets.Hash(result.Value.RefreshToken)).RevokedAt);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        var (_, tokens) = await _handlers.ARegisteredAccountAsync();
        var rotated = (await _handlers.Refresh.HandleAsync(new RefreshSession(tokens.RefreshToken), CancellationToken.None)).Value;

        var replay = await _handlers.Refresh.HandleAsync(new RefreshSession(tokens.RefreshToken), CancellationToken.None);

        ResultAssert.Fails(replay, IdentityErrors.InvalidRefreshToken);
        Assert.All(await _database.Query().RefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAt));
        var legitimate = await _handlers.Refresh.HandleAsync(new RefreshSession(rotated.RefreshToken), CancellationToken.None);
        ResultAssert.Fails(legitimate, IdentityErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Reuse_leaves_other_sessions_alone()
    {
        var (_, first) = await _handlers.ARegisteredAccountAsync();
        var second = (await _handlers.Login.HandleAsync(new Login("admin@firma.ba", IdentityHandlers.Password), CancellationToken.None)).Value;
        await _handlers.Refresh.HandleAsync(new RefreshSession(first.RefreshToken), CancellationToken.None);

        await _handlers.Refresh.HandleAsync(new RefreshSession(first.RefreshToken), CancellationToken.None);

        var result = await _handlers.Refresh.HandleAsync(new RefreshSession(second.RefreshToken), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var (_, tokens) = await _handlers.ARegisteredAccountAsync();
        _handlers.Clock.Advance(IdentityHandlers.Sessions.RefreshTokenLifetime);

        var result = await _handlers.Refresh.HandleAsync(new RefreshSession(tokens.RefreshToken), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.InvalidRefreshToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Missing_or_unknown_token_is_rejected(string? token)
    {
        var result = await _handlers.Refresh.HandleAsync(new RefreshSession(token), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Logout_revokes_the_session()
    {
        var (_, tokens) = await _handlers.ARegisteredAccountAsync();
        var rotated = (await _handlers.Refresh.HandleAsync(new RefreshSession(tokens.RefreshToken), CancellationToken.None)).Value;

        var result = await _handlers.Logout.HandleAsync(new Logout(rotated.RefreshToken), CancellationToken.None);

        Assert.True(result.IsSuccess);
        ResultAssert.Fails(
            await _handlers.Refresh.HandleAsync(new RefreshSession(rotated.RefreshToken), CancellationToken.None),
            IdentityErrors.InvalidRefreshToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    public async Task Logout_without_a_known_token_succeeds(string? token)
    {
        var result = await _handlers.Logout.HandleAsync(new Logout(token), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Change_password_replaces_hash_and_ends_all_sessions()
    {
        var (userId, tokens) = await _handlers.ARegisteredAccountAsync();

        var result = await _handlers.ChangePassword.HandleAsync(
            new ChangePassword(userId, IdentityHandlers.Password, "a brand new password"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_handlers.Hasher.Hash("a brand new password"), (await _database.Query().Users.SingleAsync()).PasswordHash);
        ResultAssert.Fails(
            await _handlers.Refresh.HandleAsync(new RefreshSession(tokens.RefreshToken), CancellationToken.None),
            IdentityErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Change_password_requires_the_current_password()
    {
        var (userId, _) = await _handlers.ARegisteredAccountAsync();

        var result = await _handlers.ChangePassword.HandleAsync(
            new ChangePassword(userId, "not the password", "a brand new password"),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "CurrentPassword", IdentityErrors.Codes.WrongPassword);
    }

    [Fact]
    public async Task Change_password_enforces_minimum_length()
    {
        var (userId, _) = await _handlers.ARegisteredAccountAsync();

        var result = await _handlers.ChangePassword.HandleAsync(new ChangePassword(userId, IdentityHandlers.Password, "short"), CancellationToken.None);

        ResultAssert.FailsValidation(result, "NewPassword", "MinimumLengthValidator");
    }

    [Fact]
    public async Task Change_password_for_unknown_user_is_not_found()
    {
        var result = await _handlers.ChangePassword.HandleAsync(
            new ChangePassword(Guid.NewGuid(), IdentityHandlers.Password, "a brand new password"),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.UserNotFound);
    }
}
