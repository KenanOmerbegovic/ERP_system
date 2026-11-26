using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Core.Domain.BusinessPartners;

namespace Erp.Modules.Core.Application.BusinessPartners;

internal static class BusinessPartnerEventMapping
{
    public static BusinessPartnerCreated ToCreatedEvent(this BusinessPartner partner, DateTimeOffset now) => new(
        Guid.CreateVersion7(), now, partner.CompanyId.Value, partner.Id, partner.Code, partner.Name, partner.TaxId, partner.VatNumber,
        partner.CountryCode, partner.IsCustomer, partner.IsSupplier, partner.IsActive);

    public static BusinessPartnerUpdated ToUpdatedEvent(this BusinessPartner partner, DateTimeOffset now) => new(
        Guid.CreateVersion7(), now, partner.CompanyId.Value, partner.Id, partner.Code, partner.Name, partner.TaxId, partner.VatNumber,
        partner.CountryCode, partner.IsCustomer, partner.IsSupplier, partner.IsActive);
}
