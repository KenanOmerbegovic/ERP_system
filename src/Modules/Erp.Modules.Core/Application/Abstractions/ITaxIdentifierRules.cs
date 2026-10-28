namespace Erp.Modules.Core.Application.Abstractions;

/// <summary>
/// Format rules for the domestic country's tax identifiers. Implemented per country
/// (Erp.Localization.BiH); Core only knows that such rules exist.
/// </summary>
public interface ITaxIdentifierRules
{
    /// <summary>ISO 3166-1 alpha-2 code of the country these rules apply to.</summary>
    string CountryCode { get; }

    /// <summary>Company / person tax identification number (BiH: JIB / ID broj).</summary>
    bool IsValidTaxId(string value);

    /// <summary>VAT registration number (BiH: PDV broj).</summary>
    bool IsValidVatNumber(string value);
}
