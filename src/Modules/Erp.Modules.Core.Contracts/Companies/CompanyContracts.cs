namespace Erp.Modules.Core.Contracts.Companies;

/// <summary>Body of create and update requests.</summary>
public sealed record CompanyRequest(
    string Name,
    string TaxId,
    string? VatNumber,
    string Address,
    string PostalCode,
    string City);

public sealed record CompanyResponse(
    Guid Id,
    string Name,
    string TaxId,
    string? VatNumber,
    string Address,
    string PostalCode,
    string City);
