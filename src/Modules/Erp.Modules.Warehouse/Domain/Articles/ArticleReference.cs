using Erp.SharedKernel.Domain;

namespace Erp.Modules.Warehouse.Domain.Articles;

/// <summary>
/// Warehouse's copy of an article Core owns, kept in sync from Core's integration events.
/// Only active goods can go on new stock documents.
/// </summary>
public sealed class ArticleReference : Entity<Guid>
{
    private ArticleReference(Guid id, CompanyId companyId, TrackingType tracking)
        : base(id, companyId)
    {
        Tracking = tracking;
    }

    private ArticleReference()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>False for services: they have no stock.</summary>
    public bool IsGoods { get; private set; }

    /// <summary>Fixed in Core when the article is created.</summary>
    public TrackingType Tracking { get; private set; }

    public Guid UnitOfMeasureId { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsStockable => IsGoods && IsActive;

    public static ArticleReference Create(
        Guid id, CompanyId companyId, string code, string name, bool isGoods, TrackingType tracking, Guid unitOfMeasureId, bool isActive)
    {
        var article = new ArticleReference(id, companyId, tracking);
        article.Update(code, name, isGoods, unitOfMeasureId, isActive);
        return article;
    }

    public void Update(string code, string name, bool isGoods, Guid unitOfMeasureId, bool isActive)
    {
        Code = Text.Required(code);
        Name = Text.Required(name);
        IsGoods = isGoods;
        UnitOfMeasureId = unitOfMeasureId;
        IsActive = isActive;
    }
}

public enum TrackingType
{
    None,
    Lot,
    Serial,
}
