using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Contracts.Users;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Users;

public sealed record ListMembers(CompanyId CompanyId);

public sealed class ListMembersHandler(IIdentityDbContext db)
{
    public async Task<IReadOnlyList<MemberResponse>> HandleAsync(ListMembers query, CancellationToken cancellationToken)
    {
        var members = await db.Memberships.AsNoTracking()
            .Where(membership => membership.CompanyId == query.CompanyId)
            .Join(db.Users, membership => membership.UserId, user => user.Id, (membership, user) => new { membership, user })
            .OrderBy(row => row.user.DisplayName)
            .ToListAsync(cancellationToken);
        return members
            .Select(row => new MemberResponse(row.user.Id, row.user.Email, row.user.DisplayName, row.membership.RoleIds, row.membership.IsActive))
            .ToList();
    }
}
