using Erp.Modules.Core.Contracts.Companies;
using Erp.Modules.Identity.Application.Auth;
using Erp.Modules.Identity.Application.Authorization;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Application.Companies;
using Erp.Modules.Identity.Application.Registration;
using Erp.Modules.Identity.Application.Roles;
using Erp.Modules.Identity.Application.Users;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Identity.Infrastructure;
using Erp.Modules.Identity.Infrastructure.Security;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Erp.Modules.Identity.Tests.TestSupport;

/// <summary>Handlers wired to one test database, plus helpers that create prerequisite data through them.</summary>
internal sealed class IdentityHandlers(IdentityDbContext db)
{
    public const string Password = "correct horse battery";
    public const string ModulePermission = "test.things.read";
    public const string OtherModulePermission = "test.things.write";

    public static readonly SessionOptions Sessions = new()
    {
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
        RefreshTokenLifetime = TimeSpan.FromDays(14),
    };

    public static readonly LockoutOptions Lockout = new() { MaxFailedAttempts = 3, Duration = TimeSpan.FromMinutes(15) };

    public static readonly RegistrationOptions Registration = new() { KeyLifetime = TimeSpan.FromDays(7) };

    public static readonly JwtOptions Jwt = new()
    {
        Issuer = "erp-test",
        Audience = "erp-test-client",
        SigningKey = "test-signing-key-at-least-thirty-two-bytes-long",
    };

    public FakeClock Clock { get; } = new();

    public FakePasswordHasher Hasher { get; } = new();

    public PermissionCatalog Catalog { get; } = new([.. IdentityPermissions.All, ModulePermission, OtherModulePermission]);

    public MemberPermissions Permissions => new(db);

    public JwtAccessTokenIssuer AccessTokens { get; } = new(Options.Create(Jwt), Options.Create(Sessions));

    private SessionIssuer SessionIssuer => new(db, AccessTokens, Options.Create(Sessions));

    public LoginHandler Login => new(db, new LoginValidator(), Hasher, SessionIssuer, Clock, Options.Create(Lockout));

    public RefreshSessionHandler Refresh => new(db, SessionIssuer, Clock);

    public LogoutHandler Logout => new(db, Clock);

    public ChangePasswordHandler ChangePassword => new(db, new ChangePasswordValidator(), Hasher, Clock);

    public IssueRegistrationKeyHandler IssueKey => new(db, new IssueRegistrationKeyValidator(), Clock, Options.Create(Registration));

    public RegisterAccountHandler RegisterAccount => new(db, new RegisterAccountValidator(), Hasher, SessionIssuer, Clock);

    public CompanyCreatedHandler CompanyCreated => new(db, NullLogger<CompanyCreatedHandler>.Instance);

    public CompanyUpdatedHandler CompanyUpdated => new(db);

    public CreateUserHandler CreateUser => new(db, new CreateUserValidator(), Hasher, Clock);

    public UpdateMemberHandler UpdateMember => new(db, new UpdateMemberValidator());

    public ListMembersHandler ListMembers => new(db);

    public CreateRoleHandler CreateRole => new(db, new CreateRoleValidator(Catalog));

    public UpdateRoleHandler UpdateRole => new(db, new UpdateRoleValidator(Catalog));

    public ListRolesHandler ListRoles => new(db);

    public GetCurrentUserHandler GetCurrentUser => new(db, Catalog, Permissions);

    public async Task<string> AKeyAsync(TimeSpan? lifetime = null) =>
        (await IssueKey.HandleAsync(new IssueRegistrationKey(lifetime), CancellationToken.None)).Value.Key;

    /// <summary>Registration step 1: an account that holds a redeemed key.</summary>
    public async Task<(Guid UserId, AuthTokens Tokens)> ARegisteredAccountAsync(string email = "admin@firma.ba")
    {
        var tokens = (await RegisterAccount.HandleAsync(
            new RegisterAccount(await AKeyAsync(), email, "Admin", Password),
            CancellationToken.None)).Value;
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        return (userId, tokens);
    }

    /// <summary>A registered company whose administrator is the returned user, as after both registration steps.</summary>
    public async Task<TestCompany> ACompanyAsync(string email = "admin@firma.ba", string name = "Firma d.o.o.")
    {
        var (userId, _) = await ARegisteredAccountAsync(email);
        var companyId = CompanyId.New();
        await CompanyCreated.HandleAsync(
            new CompanyCreated(Guid.NewGuid(), Clock.UtcNow, companyId.Value, name, userId),
            CancellationToken.None);
        var administratorRoleId = await db.Roles
            .Where(role => role.CompanyId == companyId && role.IsAdministrator)
            .Select(role => role.Id)
            .SingleAsync();
        return new TestCompany(companyId, userId, administratorRoleId);
    }

    public async Task<Guid> ARoleAsync(CompanyId companyId, string name = "Knjigovođa", params string[] permissions)
    {
        var result = await CreateRole.HandleAsync(new CreateRole(companyId, name, permissions), CancellationToken.None);
        return result.Value;
    }

    public async Task<Guid> AMemberAsync(CompanyId companyId, string email, params Guid[] roleIds)
    {
        var result = await CreateUser.HandleAsync(new CreateUser(companyId, email, "Korisnik", Password, roleIds), CancellationToken.None);
        return result.Value;
    }
}

internal sealed record TestCompany(CompanyId Id, Guid AdministratorId, Guid AdministratorRoleId);
