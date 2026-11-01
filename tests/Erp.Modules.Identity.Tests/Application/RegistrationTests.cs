using Erp.Modules.Core.Contracts.Companies;
using Erp.Modules.Identity.Application;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Application.Registration;
using Erp.Modules.Identity.Tests.TestSupport;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Tests.Application;

/// <summary>Issuing keys, step 1 (account) and step 2 (CompanyCreated) of company registration.</summary>
public sealed class RegistrationTests : IDisposable
{
    private readonly IdentityTestDatabase _database = new();
    private readonly IdentityHandlers _handlers;

    public RegistrationTests()
    {
        _handlers = new IdentityHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Issued_key_is_shown_once_and_stored_as_hash()
    {
        var result = await _handlers.IssueKey.HandleAsync(new IssueRegistrationKey(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{5}(-[0-9A-HJKMNP-TV-Z]{5}){4}$", result.Value.Key);
        Assert.Equal(_handlers.Clock.UtcNow + IdentityHandlers.Registration.KeyLifetime, result.Value.ExpiresAt);
        var stored = await _database.Query().RegistrationKeys.SingleAsync();
        Assert.Equal(Secrets.Hash(Secrets.NormalizeRegistrationKey(result.Value.Key)), stored.KeyHash);
    }

    [Fact]
    public async Task Issuer_can_choose_lifetime()
    {
        var result = await _handlers.IssueKey.HandleAsync(new IssueRegistrationKey(TimeSpan.FromDays(30)), CancellationToken.None);

        Assert.Equal(_handlers.Clock.UtcNow.AddDays(30), result.Value.ExpiresAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task Lifetime_must_be_within_a_year(int days)
    {
        var result = await _handlers.IssueKey.HandleAsync(new IssueRegistrationKey(TimeSpan.FromDays(days)), CancellationToken.None);

        Assert.IsType<Erp.SharedKernel.Results.ValidationError>(result.Error);
    }

    [Fact]
    public async Task Register_creates_account_redeems_key_and_signs_in()
    {
        var key = await _handlers.AKeyAsync();

        var result = await _handlers.RegisterAccount.HandleAsync(
            new RegisterAccount(key, " Admin@Firma.ba ", "Amra Hadžić", IdentityHandlers.Password),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = await _database.Query().Users.SingleAsync();
        Assert.Equal("admin@firma.ba", user.Email);
        Assert.Equal("Amra Hadžić", user.DisplayName);
        Assert.Equal(_handlers.Hasher.Hash(IdentityHandlers.Password), user.PasswordHash);
        var stored = await _database.Query().RegistrationKeys.SingleAsync();
        Assert.Equal(user.Id, stored.RedeemedByUserId);
        Assert.Null(stored.UsedForCompanyId);
        Assert.True(await _handlers.Permissions.CanRegisterCompanyAsync(user.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Key_can_be_typed_loosely()
    {
        var key = await _handlers.AKeyAsync();

        var result = await _handlers.RegisterAccount.HandleAsync(
            new RegisterAccount(key.Replace("-", " ").ToLowerInvariant(), "admin@firma.ba", "Admin", IdentityHandlers.Password),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unknown_key_is_rejected()
    {
        var result = await _handlers.RegisterAccount.HandleAsync(
            new RegisterAccount("AAAAA-BBBBB-CCCCC-DDDDD-EEEEE", "admin@firma.ba", "Admin", IdentityHandlers.Password),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.InvalidRegistrationKey);
        Assert.Empty(_database.Query().Users);
    }

    [Fact]
    public async Task Expired_key_is_rejected()
    {
        var key = await _handlers.AKeyAsync();
        _handlers.Clock.Advance(IdentityHandlers.Registration.KeyLifetime);

        var result = await _handlers.RegisterAccount.HandleAsync(
            new RegisterAccount(key, "admin@firma.ba", "Admin", IdentityHandlers.Password),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.InvalidRegistrationKey);
    }

    [Fact]
    public async Task Key_works_only_once()
    {
        var key = await _handlers.AKeyAsync();
        await _handlers.RegisterAccount.HandleAsync(new RegisterAccount(key, "admin@firma.ba", "Admin", IdentityHandlers.Password), CancellationToken.None);

        var result = await _handlers.RegisterAccount.HandleAsync(
            new RegisterAccount(key, "other@firma.ba", "Other", IdentityHandlers.Password),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.InvalidRegistrationKey);
    }

    [Fact]
    public async Task Email_in_use_in_another_company_is_rejected()
    {
        await _handlers.ACompanyAsync(email: "admin@firma.ba");

        var result = await _handlers.RegisterAccount.HandleAsync(
            new RegisterAccount(await _handlers.AKeyAsync(), "ADMIN@firma.ba", "Admin", IdentityHandlers.Password),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.DuplicateEmail);
    }

    [Fact]
    public async Task Register_validates_input()
    {
        var result = await _handlers.RegisterAccount.HandleAsync(
            new RegisterAccount("", "not-an-email", "", "short"),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "RegistrationKey", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "Email", "EmailValidator");
        ResultAssert.FailsValidation(result, "DisplayName", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "Password", "MinimumLengthValidator");
    }

    [Fact]
    public async Task Company_created_makes_the_registering_user_its_administrator()
    {
        var (userId, _) = await _handlers.ARegisteredAccountAsync();
        var companyId = CompanyId.New();

        await _handlers.CompanyCreated.HandleAsync(
            new CompanyCreated(Guid.NewGuid(), _handlers.Clock.UtcNow, companyId.Value, "Firma d.o.o.", userId),
            CancellationToken.None);

        var query = _database.Query();
        Assert.Equal("Firma d.o.o.", (await query.Companies.SingleAsync()).Name);
        var role = await query.Roles.SingleAsync();
        Assert.True(role.IsAdministrator);
        Assert.Equal(companyId, role.CompanyId);
        var membership = await query.Memberships.SingleAsync();
        Assert.Equal(userId, membership.UserId);
        Assert.Equal([role.Id], membership.RoleIds);
        Assert.Equal(companyId, (await query.RegistrationKeys.SingleAsync()).UsedForCompanyId);
        Assert.False(await _handlers.Permissions.CanRegisterCompanyAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task Company_created_is_idempotent()
    {
        var (userId, _) = await _handlers.ARegisteredAccountAsync();
        var created = new CompanyCreated(Guid.NewGuid(), _handlers.Clock.UtcNow, Guid.NewGuid(), "Firma d.o.o.", userId);

        await _handlers.CompanyCreated.HandleAsync(created, CancellationToken.None);
        await new IdentityHandlers(_database.Query()).CompanyCreated.HandleAsync(created, CancellationToken.None);

        Assert.Single(_database.Query().Roles);
        Assert.Single(_database.Query().Memberships);
    }

    [Fact]
    public async Task Company_created_without_an_unused_key_assigns_no_administrator()
    {
        var company = await _handlers.ACompanyAsync();

        await _handlers.CompanyCreated.HandleAsync(
            new CompanyCreated(Guid.NewGuid(), _handlers.Clock.UtcNow, Guid.NewGuid(), "Druga firma", company.AdministratorId),
            CancellationToken.None);

        Assert.Equal(2, await _database.Query().Companies.CountAsync());
        Assert.Single(_database.Query().Memberships);
    }

    [Fact]
    public async Task Company_updated_renames_the_company()
    {
        var company = await _handlers.ACompanyAsync(name: "Staro ime");

        await _handlers.CompanyUpdated.HandleAsync(
            new CompanyUpdated(Guid.NewGuid(), _handlers.Clock.UtcNow, company.Id.Value, "Novo ime"),
            CancellationToken.None);

        Assert.Equal("Novo ime", (await _database.Query().Companies.SingleAsync()).Name);
    }

    [Fact]
    public async Task Company_updated_for_unknown_company_throws_so_the_outbox_retries()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handlers.CompanyUpdated.HandleAsync(
            new CompanyUpdated(Guid.NewGuid(), _handlers.Clock.UtcNow, Guid.NewGuid(), "Novo ime"),
            CancellationToken.None));
    }
}
