namespace Erp.Modules.Identity.Application.Authorization;

/// <summary>Every permission a role can grant: Identity's own plus those of the modules the host registers.</summary>
public sealed class PermissionCatalog
{
    private readonly HashSet<string> _permissions;

    public PermissionCatalog(IEnumerable<string> permissions)
    {
        All = permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        _permissions = new HashSet<string>(All, StringComparer.Ordinal);
    }

    public IReadOnlyList<string> All { get; }

    public bool Contains(string permission) => _permissions.Contains(permission);
}
