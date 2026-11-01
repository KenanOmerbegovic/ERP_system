using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Authorization;
using Erp.Modules.Identity.Contracts.Users;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Users;

/// <summary>The signed-in user, the companies they can open and what they may do in each.</summary>
public sealed record GetCurrentUser(Guid UserId);

public sealed class GetCurrentUserHandler(IIdentityDbContext db, PermissionCatalog catalog, MemberPermissions permissions)
{
    public async Task<Result<CurrentUserResponse>> HandleAsync(GetCurrentUser query, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == query.UserId, cancellationToken);
        if (user is null)
        {
            return IdentityErrors.UserNotFound;
        }

        var memberships = await db.Memberships.AsNoTracking()
            .Where(membership => membership.UserId == user.Id && membership.IsActive)
            .ToListAsync(cancellationToken);
        var companyIds = memberships.Select(membership => membership.CompanyId).ToList();
        var roleIds = memberships.SelectMany(membership => membership.RoleIds).Distinct().ToList();
        var companies = await db.Companies.AsNoTracking()
            .Where(company => companyIds.Contains(company.Id))
            .ToDictionaryAsync(company => company.Id, cancellationToken);
        var roles = await db.Roles.AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .ToListAsync(cancellationToken);

        var memberCompanies = memberships
            .Where(membership => companies.ContainsKey(membership.CompanyId))
            .Select(membership =>
            {
                var held = roles.Where(role => role.CompanyId == membership.CompanyId && membership.RoleIds.Contains(role.Id)).ToList();
                var isAdministrator = held.Any(role => role.IsAdministrator);
                var granted = isAdministrator
                    ? catalog.All
                    : held.SelectMany(role => role.Permissions).Where(catalog.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                return new CurrentUserCompanyResponse(membership.CompanyId.Value, companies[membership.CompanyId].Name, isAdministrator, granted);
            })
            .OrderBy(company => company.Name, StringComparer.CurrentCulture)
            .ToList();

        return new CurrentUserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            memberCompanies,
            await permissions.CanRegisterCompanyAsync(user.Id, cancellationToken));
    }
}
