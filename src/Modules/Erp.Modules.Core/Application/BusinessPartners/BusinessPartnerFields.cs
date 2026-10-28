using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Domain;
using Erp.Modules.Core.Domain.BusinessPartners;
using FluentValidation;

namespace Erp.Modules.Core.Application.BusinessPartners;

public interface IBusinessPartnerFields
{
    string Code { get; }

    string Name { get; }

    string? TaxId { get; }

    string? VatNumber { get; }

    string? Address { get; }

    string? PostalCode { get; }

    string? City { get; }

    /// <summary>Empty means the domestic country.</summary>
    string? CountryCode { get; }

    bool IsCustomer { get; }

    bool IsSupplier { get; }

    bool IsActive { get; }
}

internal static class BusinessPartnerFields
{
    public static BusinessPartnerDetails ToDetails(this IBusinessPartnerFields fields, ITaxIdentifierRules taxRules) =>
        new(
            fields.Code,
            fields.Name,
            fields.TaxId,
            fields.VatNumber,
            fields.Address,
            fields.PostalCode,
            fields.City,
            string.IsNullOrWhiteSpace(fields.CountryCode) ? taxRules.CountryCode : fields.CountryCode,
            fields.IsCustomer,
            fields.IsSupplier,
            fields.IsActive);

    public static bool IsDomestic(this IBusinessPartnerFields fields, ITaxIdentifierRules taxRules) =>
        string.IsNullOrWhiteSpace(fields.CountryCode)
        || string.Equals(fields.CountryCode.Trim(), taxRules.CountryCode, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Rules shared by create and update. Tax ids of domestic partners follow local rules.</summary>
internal sealed class BusinessPartnerFieldsValidator : AbstractValidator<IBusinessPartnerFields>
{
    public BusinessPartnerFieldsValidator(ITaxIdentifierRules taxRules)
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(FieldLengths.Code);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLengths.Name);
        RuleFor(x => x.CountryCode!)
            .Matches("^ *[A-Za-z]{2} *$").WithErrorCode("CountryCode.Invalid").WithMessage("Use an ISO 3166-1 alpha-2 country code.")
            .When(x => !string.IsNullOrWhiteSpace(x.CountryCode));

        When(x => x.IsDomestic(taxRules), () =>
        {
            RuleFor(x => x.TaxId!).MustBeValidTaxId(taxRules).When(x => !string.IsNullOrWhiteSpace(x.TaxId));
            RuleFor(x => x.VatNumber!).MustBeValidVatNumber(taxRules).When(x => !string.IsNullOrWhiteSpace(x.VatNumber));
        }).Otherwise(() =>
        {
            RuleFor(x => x.TaxId).MaximumLength(FieldLengths.TaxId);
            RuleFor(x => x.VatNumber).MaximumLength(FieldLengths.VatNumber);
        });

        RuleFor(x => x.Address).MaximumLength(FieldLengths.Address);
        RuleFor(x => x.PostalCode).MaximumLength(FieldLengths.PostalCode);
        RuleFor(x => x.City).MaximumLength(FieldLengths.City);
    }
}
