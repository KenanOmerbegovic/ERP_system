using Erp.SharedKernel.Results;

namespace Erp.SharedKernel.Monetary;

/// <summary>
/// ISO 4217 currency with its number of minor units (decimal places).
/// </summary>
public sealed record Currency
{
    public static readonly Currency BAM = new("BAM", 2);
    public static readonly Currency EUR = new("EUR", 2);
    public static readonly Currency USD = new("USD", 2);
    public static readonly Currency CHF = new("CHF", 2);
    public static readonly Currency GBP = new("GBP", 2);
    public static readonly Currency RSD = new("RSD", 2);

    private static readonly Dictionary<string, Currency> ByCode =
        new[] { BAM, EUR, USD, CHF, GBP, RSD }.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    private Currency(string code, int minorUnits)
    {
        Code = code;
        MinorUnits = minorUnits;
    }

    public string Code { get; }

    public int MinorUnits { get; }

    public static Result<Currency> FromCode(string? code) =>
        code is not null && ByCode.TryGetValue(code.Trim(), out var currency)
            ? currency
            : MoneyErrors.UnknownCurrency(code);

    public override string ToString() => Code;
}
