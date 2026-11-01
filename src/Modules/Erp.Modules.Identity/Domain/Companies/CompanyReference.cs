using Erp.SharedKernel.Domain;

namespace Erp.Modules.Identity.Domain.Companies;

/// <summary>
/// Identity's copy of a company Core owns, kept in sync from Core's integration events.
/// Its <see cref="Entity{TId}.CompanyId"/> is its own id.
/// </summary>
public sealed class CompanyReference : Entity<CompanyId>
{
    private CompanyReference(CompanyId id, string name)
        : base(id, id)
    {
        Name = Text.Required(name);
    }

    private CompanyReference()
    {
    }

    public string Name { get; private set; } = null!;

    public static CompanyReference Create(CompanyId id, string name) => new(id, name);

    public void Rename(string name) => Name = Text.Required(name);
}
