using Erp.Modules.Identity.Application;
using Erp.Modules.Identity.Application.Auth;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Tests.Application;

public sealed class LoginTests : IDisposable
{
    private const string Email = "admin@firma.ba";

    private readonly IdentityTestDatabase _database = new();
    private readonly IdentityHandlers _handlers;

    public LoginTests()
    {
        _handlers = new IdentityHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Login_returns_tokens_and_stores_only_the_refresh_token_hash()
    {
        var (userId, _) = await _handlers.ARegisteredAccountAsync(Email);

        var result = await _handlers.Login.HandleAsync(new Login(Email, IdentityHandlers.Password), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        Assert.Equal(_handlers.Clock.UtcNow + IdentityHandlers.Sessions.AccessTokenLifetime, result.Value.AccessTokenExpiresAt);
        Assert.Equal(_handlers.Clock.UtcNow + IdentityHandlers.Sessions.RefreshTokenLifetime, result.Value.RefreshTokenExpiresAt);
        var stored = await _database.Query().RefreshTokens.SingleAsync(t => t.TokenHash == Secrets.Hash(result.Value.RefreshToken));
        Assert.Equal(userId, stored.UserId);
        Assert.DoesNotContain(await _database.Query().RefreshTokens.ToListAsync(), t => t.TokenHash == result.Value.RefreshToken);
    }

    [Fact]
    public async Task Login_ignores_email_case_and_whitespace()
    {
        await _handlers.ARegisteredAccountAsync(Email);

        var result = await _handlers.Login.HandleAsync(new Login("  Admin@FIRMA.ba ", IdentityHandlers.Password), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unknown_email_fails_like_a_wrong_password_and_still_verifies()
    {
        var result = await _handlers.Login.HandleAsync(new Login("nobody@firma.ba", IdentityHandlers.Password), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.InvalidCredentials);
        Assert.Equal(1, _handlers.Hasher.VerificationsWithoutUser);
    }

    [Fact]
    public async Task Wrong_password_fails_and_counts()
    {
        await _handlers.ARegisteredAccountAsync(Email);

        var result = await _handlers.Login.HandleAsync(new Login(Email, "wrong password!"), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.InvalidCredentials);
        Assert.Equal(1, (await _database.Query().Users.SingleAsync()).FailedLoginCount);
    }

    [Fact]
    public async Task Too_many_failures_lock_the_account_even_for_the_right_password()
    {
        await _handlers.ARegisteredAccountAsync(Email);
        for (var attempt = 0; attempt < IdentityHandlers.Lockout.MaxFailedAttempts; attempt++)
        {
            await _handlers.Login.HandleAsync(new Login(Email, "wrong password!"), CancellationToken.None);
        }

        var result = await _handlers.Login.HandleAsync(new Login(Email, IdentityHandlers.Password), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.LockedOut);
    }

    [Fact]
    public async Task Lockout_ends_after_its_duration()
    {
        await _handlers.ARegisteredAccountAsync(Email);
        for (var attempt = 0; attempt < IdentityHandlers.Lockout.MaxFailedAttempts; attempt++)
        {
            await _handlers.Login.HandleAsync(new Login(Email, "wrong password!"), CancellationToken.None);
        }

        _handlers.Clock.Advance(IdentityHandlers.Lockout.Duration);
        var result = await _handlers.Login.HandleAsync(new Login(Email, IdentityHandlers.Password), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = await _database.Query().Users.SingleAsync();
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public async Task Successful_login_resets_failure_count()
    {
        await _handlers.ARegisteredAccountAsync(Email);
        await _handlers.Login.HandleAsync(new Login(Email, "wrong password!"), CancellationToken.None);

        await _handlers.Login.HandleAsync(new Login(Email, IdentityHandlers.Password), CancellationToken.None);

        Assert.Equal(0, (await _database.Query().Users.SingleAsync()).FailedLoginCount);
    }

    [Fact]
    public async Task Login_requires_email_and_password()
    {
        var result = await _handlers.Login.HandleAsync(new Login("", ""), CancellationToken.None);

        ResultAssert.FailsValidation(result, "Email", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "Password", "NotEmptyValidator");
    }
}
