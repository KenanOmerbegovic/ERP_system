using Erp.SharedKernel.Domain;

namespace Erp.Modules.Identity.Domain.Memberships;

/// <summary>A user's access to one company: the roles they hold there. One per user and company.</summary>
public sealed class Membership : AggregateRoot<Guid>
{
    private Membership(Guid id, CompanyId companyId, Guid userId, IReadOnlyList<Guid> roleIds)
        : base(id, companyId)
    {
        UserId = userId;
        RoleIds = roleIds;
        IsActive = true;
    }

    private Membership()
    {
    }

    public Guid UserId { get; private init; }

    /// <summary>Roles of the same company.</summary>
    public IReadOnlyList<Guid> RoleIds { get; private set; } = [];

    /// <summary>An inactive member keeps their roles but has no access to the company.</summary>
    public bool IsActive { get; private set; }

    public static Membership Create(CompanyId companyId, Guid userId, IEnumerable<Guid> roleIds) =>
        new(Guid.CreateVersion7(), companyId, userId, roleIds.Distinct().ToList());

    public void Update(IEnumerable<Guid> roleIds, bool isActive)
    {
        RoleIds = roleIds.Distinct().ToList();
        IsActive = isActive;
    }
}
