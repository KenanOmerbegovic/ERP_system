using Erp.SharedKernel.Domain;

namespace Erp.Modules.Identity.Domain.Roles;

/// <summary>
/// A company's named set of permissions. Each company has one built-in administrator role,
/// which grants every permission (including ones added later) and cannot be changed.
/// </summary>
public sealed class Role : AggregateRoot<Guid>
{
    public const string AdministratorName = "Administrator";

    private Role(Guid id, CompanyId companyId, string name, bool isAdministrator, IReadOnlyList<string> permissions)
        : base(id, companyId)
    {
        Name = Text.Required(name);
        IsAdministrator = isAdministrator;
        Permissions = permissions;
    }

    private Role()
    {
    }

    /// <summary>Unique per company.</summary>
    public string Name { get; private set; } = null!;

    public bool IsAdministrator { get; private init; }

    /// <summary>Permission codes; empty for the administrator role, which grants all of them.</summary>
    public IReadOnlyList<string> Permissions { get; private set; } = [];

    public static Role CreateAdministrator(CompanyId companyId) =>
        new(Guid.CreateVersion7(), companyId, AdministratorName, isAdministrator: true, []);

    public static Role Create(CompanyId companyId, string name, IEnumerable<string> permissions) =>
        new(Guid.CreateVersion7(), companyId, name, isAdministrator: false, Distinct(permissions));

    public void Update(string name, IEnumerable<string> permissions)
    {
        if (IsAdministrator)
        {
            throw new InvalidOperationException("The administrator role cannot be changed.");
        }

        Name = Text.Required(name);
        Permissions = Distinct(permissions);
    }

    public bool Grants(string permission) => IsAdministrator || Permissions.Contains(permission, StringComparer.Ordinal);

    private static List<string> Distinct(IEnumerable<string> permissions) =>
        permissions.Select(permission => Text.Required(permission)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
}
