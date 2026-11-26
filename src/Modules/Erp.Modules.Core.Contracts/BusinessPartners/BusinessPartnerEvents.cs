using Erp.SharedKernel.Messaging;

namespace Erp.Modules.Core.Contracts.BusinessPartners;

/// <summary>A business partner was created. Modules that keep their own copy of partners (e.g. Purchasing's suppliers) start from this.</summary>
public sealed record BusinessPartnerCreated(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid BusinessPartnerId,
    string Code,
    string Name,
    string? TaxId,
    string? VatNumber,
    string CountryCode,
    bool IsCustomer,
    bool IsSupplier,
    bool IsActive) : IIntegrationEvent;

/// <summary>A business partner's data changed; it carries the full current state.</summary>
public sealed record BusinessPartnerUpdated(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid BusinessPartnerId,
    string Code,
    string Name,
    string? TaxId,
    string? VatNumber,
    string CountryCode,
    bool IsCustomer,
    bool IsSupplier,
    bool IsActive) : IIntegrationEvent;
