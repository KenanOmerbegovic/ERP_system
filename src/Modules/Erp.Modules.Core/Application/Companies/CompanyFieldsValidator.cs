using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Domain;
using FluentValidation;

namespace Erp.Modules.Core.Application.Companies;

public interface ICompanyFields
{
    string Name { get; }

    string TaxId { get; }

    string? VatNumber { get; }

    string Address { get; }

    string PostalCode { get; }

    string City { get; }
}

/// <summary>Rules shared by create and update. A company is always domestic, so its tax ids follow local rules.</summary>
internal sealed class CompanyFieldsValidator : AbstractValidator<ICompanyFields>
{
    public CompanyFieldsValidator(ITaxIdentifierRules taxRules)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLengths.Name);
        RuleFor(x => x.TaxId).Cascade(CascadeMode.Stop).NotEmpty().MustBeValidTaxId(taxRules);
        RuleFor(x => x.VatNumber!).MustBeValidVatNumber(taxRules)
            .When(x => !string.IsNullOrWhiteSpace(x.VatNumber));
        RuleFor(x => x.Address).NotEmpty().MaximumLength(FieldLengths.Address);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(FieldLengths.PostalCode);
        RuleFor(x => x.City).NotEmpty().MaximumLength(FieldLengths.City);
    }
}
