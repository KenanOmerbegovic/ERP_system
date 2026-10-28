using Erp.SharedKernel.Domain;

namespace Erp.Modules.Core.Domain.Companies;

/// <summary>A legal entity using the ERP; the tenant. Its <see cref="Entity{TId}.CompanyId"/> is its own id.</summary>
public sealed class Company : AggregateRoot<CompanyId>
{
    private Company(CompanyId id, string name, string taxId, string? vatNumber, string address, string postalCode, string city)
        : base(id, id)
    {
        Apply(name, taxId, vatNumber, address, postalCode, city);
    }

    private Company()
    {
    }

    public string Name { get; private set; } = null!;

    /// <summary>JIB / ID broj.</summary>
    public string TaxId { get; private set; } = null!;

    /// <summary>PDV broj; null when the company is not VAT registered.</summary>
    public string? VatNumber { get; private set; }

    public string Address { get; private set; } = null!;

    public string PostalCode { get; private set; } = null!;

    public string City { get; private set; } = null!;

    public bool IsVatRegistered => VatNumber is not null;

    public static Company Create(string name, string taxId, string? vatNumber, string address, string postalCode, string city) =>
        new(CompanyId.New(), name, taxId, vatNumber, address, postalCode, city);

    public void Update(string name, string taxId, string? vatNumber, string address, string postalCode, string city) =>
        Apply(name, taxId, vatNumber, address, postalCode, city);

    private void Apply(string name, string taxId, string? vatNumber, string address, string postalCode, string city)
    {
        Name = Text.Required(name);
        TaxId = Text.Required(taxId);
        VatNumber = Text.Optional(vatNumber);
        Address = Text.Required(address);
        PostalCode = Text.Required(postalCode);
        City = Text.Required(city);
    }
}
