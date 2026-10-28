using Erp.SharedKernel.Domain;

namespace Erp.Modules.Core.Domain.BusinessPartners;

/// <summary>A customer and/or supplier of a company.</summary>
public sealed class BusinessPartner : AggregateRoot<Guid>
{
    private BusinessPartner(Guid id, CompanyId companyId, BusinessPartnerDetails details)
        : base(id, companyId)
    {
        Apply(details);
    }

    private BusinessPartner()
    {
    }

    /// <summary>Unique per company.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>JIB / ID broj for domestic partners, foreign tax id otherwise.</summary>
    public string? TaxId { get; private set; }

    /// <summary>PDV broj; null when the partner is not VAT registered.</summary>
    public string? VatNumber { get; private set; }

    public string? Address { get; private set; }

    public string? PostalCode { get; private set; }

    public string? City { get; private set; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string CountryCode { get; private set; } = null!;

    public bool IsCustomer { get; private set; }

    public bool IsSupplier { get; private set; }

    public bool IsActive { get; private set; }

    public static BusinessPartner Create(CompanyId companyId, BusinessPartnerDetails details) =>
        new(Guid.CreateVersion7(), companyId, details);

    public void Update(BusinessPartnerDetails details) => Apply(details);

    private void Apply(BusinessPartnerDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        Code = Text.Required(details.Code);
        Name = Text.Required(details.Name);
        TaxId = Text.Optional(details.TaxId);
        VatNumber = Text.Optional(details.VatNumber);
        Address = Text.Optional(details.Address);
        PostalCode = Text.Optional(details.PostalCode);
        City = Text.Optional(details.City);
        CountryCode = Text.Required(details.CountryCode).ToUpperInvariant();
        IsCustomer = details.IsCustomer;
        IsSupplier = details.IsSupplier;
        IsActive = details.IsActive;
    }
}

public sealed record BusinessPartnerDetails(
    string Code,
    string Name,
    string? TaxId,
    string? VatNumber,
    string? Address,
    string? PostalCode,
    string? City,
    string CountryCode,
    bool IsCustomer,
    bool IsSupplier,
    bool IsActive);
