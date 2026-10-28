using Erp.SharedKernel.Domain;

namespace Erp.Modules.Core.Domain.UnitsOfMeasure;

public sealed class UnitOfMeasure : AggregateRoot<Guid>
{
    private UnitOfMeasure(Guid id, CompanyId companyId, string code, string name)
        : base(id, companyId)
    {
        Update(code, name);
    }

    private UnitOfMeasure()
    {
    }

    /// <summary>Short symbol such as "kom" or "kg"; unique per company.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public static UnitOfMeasure Create(CompanyId companyId, string code, string name) =>
        new(Guid.CreateVersion7(), companyId, code, name);

    public void Update(string code, string name)
    {
        Code = Text.Required(code);
        Name = Text.Required(name);
    }
}
