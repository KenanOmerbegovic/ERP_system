using Erp.SharedKernel.Domain;

namespace Erp.Modules.Purchasing.Domain.References;

/// <summary>
/// Purchasing's copy of a Core business partner, kept in sync from Core's events. Every partner is kept, because the
/// supplier flag can change; only an active supplier can be ordered from.
/// </summary>
public sealed class SupplierReference : Entity<Guid>
{
    private SupplierReference(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private SupplierReference()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? TaxId { get; private set; }

    public string? VatNumber { get; private set; }

    public bool IsSupplier { get; private set; }

    public bool IsActive { get; private set; }

    public bool CanBeOrderedFrom => IsSupplier && IsActive;

    public static SupplierReference Create(Guid id, CompanyId companyId, string code, string name, string? taxId, string? vatNumber, bool isSupplier, bool isActive)
    {
        var supplier = new SupplierReference(id, companyId);
        supplier.Update(code, name, taxId, vatNumber, isSupplier, isActive);
        return supplier;
    }

    public void Update(string code, string name, string? taxId, string? vatNumber, bool isSupplier, bool isActive)
    {
        Code = Text.Required(code);
        Name = Text.Required(name);
        TaxId = Text.Optional(taxId);
        VatNumber = Text.Optional(vatNumber);
        IsSupplier = isSupplier;
        IsActive = isActive;
    }
}

/// <summary>Purchasing's copy of a Core article, kept in sync from Core's events. Only active goods are ordered.</summary>
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

    public bool IsGoods { get; private set; }

    /// <summary>Fixed in Core when the article is created.</summary>
    public TrackingType Tracking { get; private set; }

    public string? DefaultVatCode { get; private set; }

    public bool IsActive { get; private set; }

    public bool CanBeOrdered => IsGoods && IsActive;

    public static ArticleReference Create(
        Guid id, CompanyId companyId, string code, string name, bool isGoods, TrackingType tracking, string? defaultVatCode, bool isActive)
    {
        var article = new ArticleReference(id, companyId, tracking);
        article.Update(code, name, isGoods, defaultVatCode, isActive);
        return article;
    }

    public void Update(string code, string name, bool isGoods, string? defaultVatCode, bool isActive)
    {
        Code = Text.Required(code);
        Name = Text.Required(name);
        IsGoods = isGoods;
        DefaultVatCode = Text.Optional(defaultVatCode);
        IsActive = isActive;
    }
}

public enum TrackingType
{
    None,
    Lot,
    Serial,
}
