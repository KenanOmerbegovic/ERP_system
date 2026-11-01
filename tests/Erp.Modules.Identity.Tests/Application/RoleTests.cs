using Erp.Modules.Identity.Application;
using Erp.Modules.Identity.Application.Roles;
using Erp.Modules.Identity.Domain.Roles;
using Erp.Modules.Identity.Tests.TestSupport;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Tests.Application;

public sealed class RoleTests : IDisposable
{
    private readonly IdentityTestDatabase _database = new();
    private readonly IdentityHandlers _handlers;

    public RoleTests()
    {
        _handlers = new IdentityHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Create_saves_trimmed_name_and_distinct_permissions()
    {
        var company = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateRole.HandleAsync(
            new CreateRole(company.Id, " Knjigovođa ", [IdentityHandlers.OtherModulePermission, IdentityHandlers.ModulePermission, IdentityHandlers.ModulePermission]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var role = await _database.Query().Roles.SingleAsync(r => r.Id == result.Value);
        Assert.Equal("Knjigovođa", role.Name);
        Assert.False(role.IsAdministrator);
        Assert.Equal([IdentityHandlers.ModulePermission, IdentityHandlers.OtherModulePermission], role.Permissions);
    }

    [Fact]
    public async Task Create_rejects_unknown_permission()
    {
        var company = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateRole.HandleAsync(new CreateRole(company.Id, "Knjigovođa", ["made.up"]), CancellationToken.None);

        ResultAssert.FailsValidation(result, "Permissions", IdentityErrors.Codes.UnknownPermission);
    }

    [Fact]
    public async Task Create_rejects_duplicate_name_in_the_company()
    {
        var company = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateRole.HandleAsync(new CreateRole(company.Id, Role.AdministratorName, []), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.DuplicateRoleName);
    }

    [Fact]
    public async Task Same_name_is_fine_in_another_company()
    {
        var company = await _handlers.ACompanyAsync(email: "admin@firma.ba");
        var other = await _handlers.ACompanyAsync(email: "admin@druga.ba", name: "Druga firma");
        await _handlers.ARoleAsync(company.Id, "Knjigovođa");

        var result = await _handlers.CreateRole.HandleAsync(new CreateRole(other.Id, "Knjigovođa", []), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_in_unknown_company_is_not_found()
    {
        var result = await _handlers.CreateRole.HandleAsync(new CreateRole(CompanyId.New(), "Knjigovođa", []), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.CompanyNotFound);
    }

    [Fact]
    public async Task Update_changes_name_and_permissions()
    {
        var company = await _handlers.ACompanyAsync();
        var roleId = await _handlers.ARoleAsync(company.Id, "Knjigovođa", IdentityHandlers.ModulePermission);

        var result = await _handlers.UpdateRole.HandleAsync(
            new UpdateRole(company.Id, roleId, "Računovođa", [IdentityHandlers.OtherModulePermission]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var role = await _database.Query().Roles.SingleAsync(r => r.Id == roleId);
        Assert.Equal("Računovođa", role.Name);
        Assert.Equal([IdentityHandlers.OtherModulePermission], role.Permissions);
    }

    [Fact]
    public async Task Administrator_role_is_read_only()
    {
        var company = await _handlers.ACompanyAsync();

        var result = await _handlers.UpdateRole.HandleAsync(
            new UpdateRole(company.Id, company.AdministratorRoleId, "Šef", []),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.AdministratorRoleIsReadOnly);
    }

    [Fact]
    public async Task Update_of_another_companys_role_is_not_found()
    {
        var company = await _handlers.ACompanyAsync(email: "admin@firma.ba");
        var other = await _handlers.ACompanyAsync(email: "admin@druga.ba", name: "Druga firma");
        var roleId = await _handlers.ARoleAsync(other.Id);

        var result = await _handlers.UpdateRole.HandleAsync(new UpdateRole(company.Id, roleId, "Preuzeto", []), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.RoleNotFound);
    }

    [Fact]
    public async Task Update_rejects_name_of_another_role()
    {
        var company = await _handlers.ACompanyAsync();
        await _handlers.ARoleAsync(company.Id, "Knjigovođa");
        var roleId = await _handlers.ARoleAsync(company.Id, "Skladištar");

        var result = await _handlers.UpdateRole.HandleAsync(new UpdateRole(company.Id, roleId, "Knjigovođa", []), CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.DuplicateRoleName);
    }

    [Fact]
    public async Task List_puts_administrator_first_then_by_name()
    {
        var company = await _handlers.ACompanyAsync();
        await _handlers.ARoleAsync(company.Id, "Skladištar");
        await _handlers.ARoleAsync(company.Id, "Knjigovođa");

        var roles = await _handlers.ListRoles.HandleAsync(new ListRoles(company.Id), CancellationToken.None);

        Assert.Equal([Role.AdministratorName, "Knjigovođa", "Skladištar"], roles.Select(r => r.Name));
        Assert.True(roles[0].IsAdministrator);
    }
}
