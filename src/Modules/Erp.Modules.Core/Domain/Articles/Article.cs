using Erp.SharedKernel.Domain;

namespace Erp.Modules.Core.Domain.Articles;

/// <summary>Goods or a service a company buys or sells. Tax is linked later, from Finance configuration.</summary>
public sealed class Article : AggregateRoot<Guid>
{
    private Article(Guid id, CompanyId companyId, string code, string name, ArticleType type, Guid unitOfMeasureId, bool isActive)
        : base(id, companyId)
    {
        Update(code, name, type, unitOfMeasureId, isActive);
    }

    private Article()
    {
    }

    /// <summary>Unique per company.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public ArticleType Type { get; private set; }

    /// <summary>A unit of measure of the same company.</summary>
    public Guid UnitOfMeasureId { get; private set; }

    public bool IsActive { get; private set; }

    public static Article Create(CompanyId companyId, string code, string name, ArticleType type, Guid unitOfMeasureId, bool isActive) =>
        new(Guid.CreateVersion7(), companyId, code, name, type, unitOfMeasureId, isActive);

    public void Update(string code, string name, ArticleType type, Guid unitOfMeasureId, bool isActive)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown article type.");
        }

        if (unitOfMeasureId == Guid.Empty)
        {
            throw new ArgumentException("Article needs a unit of measure.", nameof(unitOfMeasureId));
        }

        Code = Text.Required(code);
        Name = Text.Required(name);
        Type = type;
        UnitOfMeasureId = unitOfMeasureId;
        IsActive = isActive;
    }
}

public enum ArticleType
{
    Goods,
    Service,
}
