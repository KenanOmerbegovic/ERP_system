using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Domain.Roles;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Authorization;

/// <summary>
/// What a user may do in a company, read from the database on every check so role and
/// membership changes apply immediately rather than when the access token expires.
/// </summary>
public sealed class MemberPermissions(IIdentityDbContext db)
{
    public async Task<bool> HasPermissionAsync(Guid userId, CompanyId companyId, string permission, CancellationToken cancellationToken)
    {
        var roles = await RolesInAsync(userId, companyId, cancellationToken);
        return roles.Any(role => role.Grants(permission));
    }

    /// <summary>Roles of the user's active membership in the company; empty without one.</summary>
    public async Task<IReadOnlyList<Role>> RolesInAsync(Guid userId, CompanyId companyId, CancellationToken cancellationToken)
    {
        var roleIds = await db.Memberships.AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.CompanyId == companyId && membership.IsActive)
            .Select(membership => membership.RoleIds)
            .FirstOrDefaultAsync(cancellationToken);
        if (roleIds is null || roleIds.Count == 0)
        {
            return [];
        }

        return await db.Roles.AsNoTracking()
            .Where(role => role.CompanyId == companyId && roleIds.Contains(role.Id))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The user redeemed a registration key whose company is not created yet.</summary>
    public Task<bool> CanRegisterCompanyAsync(Guid userId, CancellationToken cancellationToken) =>
        db.RegistrationKeys.AnyAsync(key => key.RedeemedByUserId == userId && key.UsedForCompanyId == null, cancellationToken);
}
