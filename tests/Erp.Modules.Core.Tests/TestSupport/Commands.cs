using Erp.Modules.Core.Application.BusinessPartners;
using Erp.Modules.Core.Application.Companies;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Core.Tests.TestSupport;

/// <summary>Valid commands with overridable fields.</summary>
internal static class Commands
{
    /// <summary>The user registering companies in tests.</summary>
    public static readonly Guid RegisteringUserId = Guid.Parse("0190a000-0000-7000-8000-000000000001");

    public static CreateCompany CreateCompany(
        string name = "Firma d.o.o.",
        string taxId = "4200000000001",
        string? vatNumber = "200000000001",
        string address = "Ferhadija 1",
        string postalCode = "71000",
        string city = "Sarajevo") =>
        new(RegisteringUserId, name, taxId, vatNumber, address, postalCode, city);

    public static UpdateCompany UpdateCompany(
        CompanyId companyId,
        string name = "Firma d.o.o.",
        string taxId = "4200000000001",
        string? vatNumber = "200000000001",
        string address = "Ferhadija 1",
        string postalCode = "71000",
        string city = "Sarajevo") =>
        new(companyId, name, taxId, vatNumber, address, postalCode, city);

    public static CreateBusinessPartner CreateBusinessPartner(
        CompanyId companyId,
        string code = "K001",
        string name = "Kupac d.o.o.",
        string? taxId = "4200000000099",
        string? vatNumber = null,
        string? countryCode = null,
        bool isCustomer = true,
        bool isSupplier = false) =>
        new(companyId, code, name, taxId, vatNumber, "Titova 5", "71000", "Sarajevo", countryCode, isCustomer, isSupplier, IsActive: true);

    public static UpdateBusinessPartner UpdateBusinessPartner(
        CompanyId companyId,
        Guid businessPartnerId,
        string code = "K001",
        string name = "Kupac d.o.o.",
        string? taxId = "4200000000099",
        string? countryCode = null,
        bool isActive = true) =>
        new(companyId, businessPartnerId, code, name, taxId, null, "Titova 5", "71000", "Sarajevo", countryCode, true, false, isActive);
}
