using Erp.SharedKernel.Domain;

namespace Erp.Modules.Core.Domain.Articles;

/// <summary>Goods or a service a company buys or sells. Tax is linked later, from Finance configuration.</summary>
public sealed class Article : AggregateRoot<Guid>
{
    private Article(
        Guid id, CompanyId companyId, string code, string name, ArticleType type, TrackingType trackingType, Guid unitOfMeasureId, string? defaultVatCode, bool isActive)
        : base(id, companyId)
    {
        if (!Enum.IsDefined(trackingType))
        {
            throw new ArgumentOutOfRangeException(nameof(trackingType), trackingType, "Unknown tracking type.");
        }

        TrackingType = trackingType;
        Update(code, name, type, unitOfMeasureId, defaultVatCode, isActive);
    }

    private Article()
    {
    }

    /// <summary>Unique per company.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public ArticleType Type { get; private set; }

    /// <summary>How stock of the article is identified. Fixed at creation: stock already held would lose or lack its LOT or serial numbers.</summary>
    public TrackingType TrackingType { get; private set; }

    /// <summary>A unit of measure of the same company.</summary>
    public Guid UnitOfMeasureId { get; private set; }

    /// <summary>The VAT code purchase and sales lines start from; one of the company's VAT rates. Optional.</summary>
    public string? DefaultVatCode { get; private set; }

    public bool IsActive { get; private set; }

    public static Article Create(
        CompanyId companyId, string code, string name, ArticleType type, TrackingType trackingType, Guid unitOfMeasureId, string? defaultVatCode, bool isActive) =>
        new(Guid.CreateVersion7(), companyId, code, name, type, trackingType, unitOfMeasureId, defaultVatCode, isActive);

    public void Update(string code, string name, ArticleType type, Guid unitOfMeasureId, string? defaultVatCode, bool isActive)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown article type.");
        }

        if (unitOfMeasureId == Guid.Empty)
        {
            throw new ArgumentException("Article needs a unit of measure.", nameof(unitOfMeasureId));
        }

        if (type == ArticleType.Service && TrackingType != TrackingType.None)
        {
            throw new ArgumentException("A service has no stock, so it cannot be LOT or serial tracked.", nameof(type));
        }

        Code = Text.Required(code);
        Name = Text.Required(name);
        Type = type;
        UnitOfMeasureId = unitOfMeasureId;
        DefaultVatCode = Text.Optional(defaultVatCode);
        IsActive = isActive;
    }
}

public enum ArticleType
{
    Goods,
    Service,
}

public enum TrackingType
{
    None,
    Lot,
    Serial,
}
