using Erp.Modules.Identity.Application;
using Erp.Modules.Identity.Application.Users;
using Erp.Modules.Identity.Tests.TestSupport;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Identity.Tests.Application;

/// <summary>What members may do (MemberPermissions) and what GET /api/me reports.</summary>
public sealed class PermissionTests : IDisposable
{
    private readonly IdentityTestDatabase _database = new();
    private readonly IdentityHandlers _handlers;

    public PermissionTests()
    {
        _handlers = new IdentityHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Role_grants_its_permissions_only()
    {
        var company = await _handlers.ACompanyAsync();
        var roleId = await _handlers.ARoleAsync(company.Id, "Knjigovođa", IdentityHandlers.ModulePermission);
        var userId = await _handlers.AMemberAsync(company.Id, "emir@firma.ba", roleId);

        Assert.True(await HasAsync(userId, company.Id, IdentityHandlers.ModulePermission));
        Assert.False(await HasAsync(userId, company.Id, IdentityHandlers.OtherModulePermission));
    }

    [Fact]
    public async Task Administrator_has_every_permission()
    {
        var company = await _handlers.ACompanyAsync();

        Assert.True(await HasAsync(company.AdministratorId, company.Id, IdentityHandlers.OtherModulePermission));
    }

    [Fact]
    public async Task Membership_grants_nothing_in_another_company()
    {
        var company = await _handlers.ACompanyAsync(email: "admin@firma.ba");
        var other = await _handlers.ACompanyAsync(email: "admin@druga.ba", name: "Druga firma");

        Assert.False(await HasAsync(company.AdministratorId, other.Id, IdentityHandlers.ModulePermission));
    }

    [Fact]
    public async Task Inactive_member_has_no_permissions()
    {
        var company = await _handlers.ACompanyAsync();
        var userId = await _handlers.AMemberAsync(company.Id, "emir@firma.ba", company.AdministratorRoleId);
        await _handlers.UpdateMember.HandleAsync(new UpdateMember(company.Id, userId, [company.AdministratorRoleId], IsActive: false), CancellationToken.None);

        Assert.False(await HasAsync(userId, company.Id, IdentityHandlers.ModulePermission));
    }

    [Fact]
    public async Task Role_changes_apply_immediately()
    {
        var company = await _handlers.ACompanyAsync();
        var roleId = await _handlers.ARoleAsync(company.Id, "Knjigovođa", IdentityHandlers.ModulePermission);
        var userId = await _handlers.AMemberAsync(company.Id, "emir@firma.ba", roleId);

        await _handlers.UpdateRole.HandleAsync(
            new Identity.Application.Roles.UpdateRole(company.Id, roleId, "Knjigovođa", []),
            CancellationToken.None);

        Assert.False(await HasAsync(userId, company.Id, IdentityHandlers.ModulePermission));
    }

    [Fact]
    public async Task Current_user_lists_companies_with_granted_permissions()
    {
        var company = await _handlers.ACompanyAsync(name: "Firma d.o.o.");
        var roleId = await _handlers.ARoleAsync(company.Id, "Knjigovođa", IdentityHandlers.ModulePermission);
        var userId = await _handlers.AMemberAsync(company.Id, "emir@firma.ba", roleId);

        var result = await _handlers.GetCurrentUser.HandleAsync(new GetCurrentUser(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("emir@firma.ba", result.Value.Email);
        var membership = Assert.Single(result.Value.Companies);
        Assert.Equal(company.Id.Value, membership.CompanyId);
        Assert.Equal("Firma d.o.o.", membership.Name);
        Assert.False(membership.IsAdministrator);
        Assert.Equal([IdentityHandlers.ModulePermission], membership.Permissions);
        Assert.False(result.Value.CanRegisterCompany);
    }

    [Fact]
    public async Task Current_user_administrator_gets_the_whole_catalog()
    {
        var company = await _handlers.ACompanyAsync();

        var result = await _handlers.GetCurrentUser.HandleAsync(new GetCurrentUser(company.AdministratorId), CancellationToken.None);

        var membership = Assert.Single(result.Value.Companies);
        Assert.True(membership.IsAdministrator);
        Assert.Equal(_handlers.Catalog.All, membership.Permissions);
    }

    [Fact]
    public async Task Registered_account_without_company_can_register_one()
    {
        var (userId, _) = await _handlers.ARegisteredAccountAsync();

        var result = await _handlers.GetCurrentUser.HandleAsync(new GetCurrentUser(userId), CancellationToken.None);

        Assert.Empty(result.Value.Companies);
        Assert.True(result.Value.CanRegisterCompany);
    }

    [Fact]
    public async Task Unknown_current_user_is_not_found()
    {
        var result = await _handlers.GetCurrentUser.HandleAsync(new GetCurrentUser(Guid.NewGuid()), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.UserNotFound);
    }

    private Task<bool> HasAsync(Guid userId, CompanyId companyId, string permission) =>
        _handlers.Permissions.HasPermissionAsync(userId, companyId, permission, CancellationToken.None);
}
