namespace Erp.Modules.Core.Contracts.BusinessPartners;

/// <summary>Body of create and update requests.</summary>
public sealed record BusinessPartnerRequest(
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

public sealed record BusinessPartnerResponse(
    Guid Id,
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
