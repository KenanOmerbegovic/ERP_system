using Erp.Modules.Core.Application.Abstractions;

namespace Erp.Modules.Core.Tests.TestSupport;

/// <summary>Accepts every identifier except <see cref="Invalid"/>, so tests show Core consults the rules.</summary>
internal sealed class FakeTaxIdentifierRules : ITaxIdentifierRules
{
    public const string Domestic = "BA";
    public const string Invalid = "INVALID";

    public static readonly FakeTaxIdentifierRules Instance = new();

    public string CountryCode => Domestic;

    public bool IsValidTaxId(string value) => value != Invalid;

    public bool IsValidVatNumber(string value) => value != Invalid;
}
