using Erp.SharedKernel.Domain;

namespace Erp.Modules.Warehouse.Domain.Storehouses;

/// <summary>
/// A warehouse (skladište) that holds stock. Named Storehouse in code because <c>Erp.Modules.Warehouse</c>
/// is the module's namespace; the API and UI call it a warehouse.
/// </summary>
public sealed class Storehouse : AggregateRoot<Guid>
{
    private Storehouse(Guid id, CompanyId companyId, string code, string name, bool isActive)
        : base(id, companyId)
    {
        Update(code, name, isActive);
    }

    private Storehouse()
    {
    }

    /// <summary>Unique per company.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>Inactive warehouses cannot be chosen on new documents; their stock stays.</summary>
    public bool IsActive { get; private set; }

    public static Storehouse Create(CompanyId companyId, string code, string name, bool isActive) =>
        new(Guid.CreateVersion7(), companyId, code, name, isActive);

    public void Update(string code, string name, bool isActive)
    {
        Code = Text.Required(code);
        Name = Text.Required(name);
        IsActive = isActive;
    }
}
