using Erp.Modules.Core.Application.Abstractions;

namespace Erp.Localization.BiH.Core;

/// <summary>
/// Bosnia and Herzegovina: JIB / ID broj has 13 digits, PDV broj has 12 digits.
/// Format only; no checksum is verified.
/// </summary>
public sealed class BiHTaxIdentifierRules : ITaxIdentifierRules
{
    public const int TaxIdLength = 13;
    public const int VatNumberLength = 12;

    public string CountryCode => "BA";

    public bool IsValidTaxId(string value) => IsDigits(value, TaxIdLength);

    public bool IsValidVatNumber(string value) => IsDigits(value, VatNumberLength);

    private static bool IsDigits(string value, int length) =>
        value is not null && value.Length == length && value.All(char.IsAsciiDigit);
}
