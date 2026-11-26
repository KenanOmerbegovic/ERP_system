using Erp.SharedKernel.Domain;

namespace Erp.Modules.Sales.Domain.References;

/// <summary>
/// Sales' copy of a Core business partner, kept in sync from Core's events. Every partner is kept, because the customer
/// flag can change; only an active customer can be offered or sold to.
/// </summary>
public sealed class CustomerReference : Entity<Guid>
{
    private CustomerReference(Guid id, CompanyId companyId)
        : base(id, companyId)
    {
    }

    private CustomerReference()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? TaxId { get; private set; }

    public string? VatNumber { get; private set; }

    public bool IsCustomer { get; private set; }

    public bool IsActive { get; private set; }

    public bool CanBeSoldTo => IsCustomer && IsActive;

    public static CustomerReference Create(Guid id, CompanyId companyId, string code, string name, string? taxId, string? vatNumber, bool isCustomer, bool isActive)
    {
        var customer = new CustomerReference(id, companyId);
        customer.Update(code, name, taxId, vatNumber, isCustomer, isActive);
        return customer;
    }

    public void Update(string code, string name, string? taxId, string? vatNumber, bool isCustomer, bool isActive)
    {
        Code = Text.Required(code);
        Name = Text.Required(name);
        TaxId = Text.Optional(taxId);
        VatNumber = Text.Optional(vatNumber);
        IsCustomer = isCustomer;
        IsActive = isActive;
    }
}

/// <summary>Sales' copy of a Core article, kept in sync from Core's events. Active goods and services can be sold.</summary>
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

    /// <summary>Services are invoiced from the order, without a delivery note.</summary>
    public bool IsService { get; private set; }

    /// <summary>Fixed in Core when the article is created.</summary>
    public TrackingType Tracking { get; private set; }

    public string? DefaultVatCode { get; private set; }

    public bool IsActive { get; private set; }

    public static ArticleReference Create(
        Guid id, CompanyId companyId, string code, string name, bool isService, TrackingType tracking, string? defaultVatCode, bool isActive)
    {
        var article = new ArticleReference(id, companyId, tracking);
        article.Update(code, name, isService, defaultVatCode, isActive);
        return article;
    }

    public void Update(string code, string name, bool isService, string? defaultVatCode, bool isActive)
    {
        Code = Text.Required(code);
        Name = Text.Required(name);
        IsService = isService;
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
