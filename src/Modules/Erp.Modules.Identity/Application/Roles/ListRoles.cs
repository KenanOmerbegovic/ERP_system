using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Contracts.Roles;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Roles;

public sealed record ListRoles(CompanyId CompanyId);

public sealed class ListRolesHandler(IIdentityDbContext db)
{
    /// <summary>The administrator role first, then by name.</summary>
    public async Task<IReadOnlyList<RoleResponse>> HandleAsync(ListRoles query, CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking()
            .Where(role => role.CompanyId == query.CompanyId)
            .OrderByDescending(role => role.IsAdministrator)
            .ThenBy(role => role.Name)
            .ToListAsync(cancellationToken);
        return roles.Select(role => new RoleResponse(role.Id, role.Name, role.IsAdministrator, role.Permissions)).ToList();
    }
}
