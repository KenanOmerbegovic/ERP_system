using Erp.Modules.Identity.Application;
using Erp.Modules.Identity.Application.Users;
using Erp.Modules.Identity.Tests.TestSupport;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Tests.Application;

public sealed class UserTests : IDisposable
{
    private readonly IdentityTestDatabase _database = new();
    private readonly IdentityHandlers _handlers;

    public UserTests()
    {
        _handlers = new IdentityHandlers(_database.Db);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Create_adds_login_and_membership_with_roles()
    {
        var company = await _handlers.ACompanyAsync();
        var roleId = await _handlers.ARoleAsync(company.Id, "Knjigovođa", IdentityHandlers.ModulePermission);

        var result = await _handlers.CreateUser.HandleAsync(
            new CreateUser(company.Id, " Emir@Firma.ba", "Emir Kovač", IdentityHandlers.Password, [roleId]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = await _database.Query().Users.SingleAsync(u => u.Id == result.Value);
        Assert.Equal("emir@firma.ba", user.Email);
        Assert.Equal(_handlers.Hasher.Hash(IdentityHandlers.Password), user.PasswordHash);
        var membership = await _database.Query().Memberships.SingleAsync(m => m.UserId == result.Value);
        Assert.Equal(company.Id, membership.CompanyId);
        Assert.Equal([roleId], membership.RoleIds);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task Create_rejects_email_used_anywhere()
    {
        var company = await _handlers.ACompanyAsync(email: "admin@firma.ba");
        await _handlers.ACompanyAsync(email: "admin@druga.ba", name: "Druga firma");

        var result = await _handlers.CreateUser.HandleAsync(
            new CreateUser(company.Id, "admin@druga.ba", "Admin", IdentityHandlers.Password, []),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.DuplicateEmail);
    }

    [Fact]
    public async Task Create_rejects_role_of_another_company()
    {
        var company = await _handlers.ACompanyAsync(email: "admin@firma.ba");
        var other = await _handlers.ACompanyAsync(email: "admin@druga.ba", name: "Druga firma");

        var result = await _handlers.CreateUser.HandleAsync(
            new CreateUser(company.Id, "emir@firma.ba", "Emir", IdentityHandlers.Password, [other.AdministratorRoleId]),
            CancellationToken.None);

        ResultAssert.FailsValidation(result, "RoleIds", IdentityErrors.Codes.UnknownRole);
    }

    [Fact]
    public async Task Create_in_unknown_company_is_not_found()
    {
        var result = await _handlers.CreateUser.HandleAsync(
            new CreateUser(CompanyId.New(), "emir@firma.ba", "Emir", IdentityHandlers.Password, []),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.CompanyNotFound);
    }

    [Fact]
    public async Task Create_validates_input()
    {
        var company = await _handlers.ACompanyAsync();

        var result = await _handlers.CreateUser.HandleAsync(new CreateUser(company.Id, "", " ", "short", []), CancellationToken.None);

        ResultAssert.FailsValidation(result, "Email", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "DisplayName", "NotEmptyValidator");
        ResultAssert.FailsValidation(result, "Password", "MinimumLengthValidator");
    }

    [Fact]
    public async Task Update_changes_roles_and_active_flag()
    {
        var company = await _handlers.ACompanyAsync();
        var roleId = await _handlers.ARoleAsync(company.Id, "Knjigovođa", IdentityHandlers.ModulePermission);
        var userId = await _handlers.AMemberAsync(company.Id, "emir@firma.ba", roleId);

        var result = await _handlers.UpdateMember.HandleAsync(
            new UpdateMember(company.Id, userId, [company.AdministratorRoleId], IsActive: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var membership = await _database.Query().Memberships.SingleAsync(m => m.UserId == userId);
        Assert.Equal([company.AdministratorRoleId], membership.RoleIds);
        Assert.False(membership.IsActive);
    }

    [Fact]
    public async Task The_last_administrator_cannot_lose_the_role()
    {
        var company = await _handlers.ACompanyAsync();

        var result = await _handlers.UpdateMember.HandleAsync(
            new UpdateMember(company.Id, company.AdministratorId, [], IsActive: true),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.LastAdministrator);
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_deactivated()
    {
        var company = await _handlers.ACompanyAsync();

        var result = await _handlers.UpdateMember.HandleAsync(
            new UpdateMember(company.Id, company.AdministratorId, [company.AdministratorRoleId], IsActive: false),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.LastAdministrator);
    }

    [Fact]
    public async Task An_administrator_can_step_down_when_another_remains()
    {
        var company = await _handlers.ACompanyAsync();
        await _handlers.AMemberAsync(company.Id, "drugi.admin@firma.ba", company.AdministratorRoleId);

        var result = await _handlers.UpdateMember.HandleAsync(
            new UpdateMember(company.Id, company.AdministratorId, [], IsActive: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Inactive_administrators_do_not_count()
    {
        var company = await _handlers.ACompanyAsync();
        var second = await _handlers.AMemberAsync(company.Id, "drugi.admin@firma.ba", company.AdministratorRoleId);
        await _handlers.UpdateMember.HandleAsync(new UpdateMember(company.Id, second, [company.AdministratorRoleId], IsActive: false), CancellationToken.None);

        var result = await _handlers.UpdateMember.HandleAsync(
            new UpdateMember(company.Id, company.AdministratorId, [], IsActive: true),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.LastAdministrator);
    }

    [Fact]
    public async Task Update_of_non_member_is_not_found()
    {
        var company = await _handlers.ACompanyAsync(email: "admin@firma.ba");
        var other = await _handlers.ACompanyAsync(email: "admin@druga.ba", name: "Druga firma");

        var result = await _handlers.UpdateMember.HandleAsync(
            new UpdateMember(company.Id, other.AdministratorId, [], IsActive: true),
            CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrors.MemberNotFound);
    }

    [Fact]
    public async Task Update_rejects_unknown_role()
    {
        var company = await _handlers.ACompanyAsync();
        var userId = await _handlers.AMemberAsync(company.Id, "emir@firma.ba");

        var result = await _handlers.UpdateMember.HandleAsync(new UpdateMember(company.Id, userId, [Guid.NewGuid()], IsActive: true), CancellationToken.None);

        ResultAssert.FailsValidation(result, "RoleIds", IdentityErrors.Codes.UnknownRole);
    }

    [Fact]
    public async Task List_returns_only_members_of_the_company()
    {
        var company = await _handlers.ACompanyAsync(email: "admin@firma.ba");
        await _handlers.AMemberAsync(company.Id, "emir@firma.ba");
        await _handlers.ACompanyAsync(email: "admin@druga.ba", name: "Druga firma");

        var members = await _handlers.ListMembers.HandleAsync(new ListMembers(company.Id), CancellationToken.None);

        Assert.Equal(["admin@firma.ba", "emir@firma.ba"], members.Select(m => m.Email).Order());
    }
}
