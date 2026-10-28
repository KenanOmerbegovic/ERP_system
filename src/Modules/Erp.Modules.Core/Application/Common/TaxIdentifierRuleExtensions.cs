using Erp.Modules.Core.Application.Abstractions;
using FluentValidation;

namespace Erp.Modules.Core.Application.Common;

internal static class TaxIdentifierRuleExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeValidTaxId<T>(this IRuleBuilder<T, string> rule, ITaxIdentifierRules taxRules) =>
        rule.Must(value => taxRules.IsValidTaxId(value.Trim()))
            .WithErrorCode("TaxId.Invalid")
            .WithMessage("Invalid tax id.");

    public static IRuleBuilderOptions<T, string> MustBeValidVatNumber<T>(this IRuleBuilder<T, string> rule, ITaxIdentifierRules taxRules) =>
        rule.Must(value => taxRules.IsValidVatNumber(value.Trim()))
            .WithErrorCode("VatNumber.Invalid")
            .WithMessage("Invalid VAT number.");
}
