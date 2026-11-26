namespace Erp.Modules.Sales.Domain;

/// <summary>Normalizes text fields. Input is validated before it reaches the domain, so blanks here are bugs.</summary>
internal static class Text
{
    public static string Required(string value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }

    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Maximum text lengths and decimal precision, shared by validation and the database schema.</summary>
public static class FieldLengths
{
    public const int Code = 50;
    public const int Name = 200;
    public const int Note = 500;
    public const int DocumentNumber = 50;
    public const int NumberPrefix = 20;
    public const int LotNumber = 50;
    public const int SerialNumber = 100;
    public const int VatCode = 20;
    public const int TaxId = 20;

    /// <summary>Quantities and prices: numeric(18,4).</summary>
    public const int QuantityPrecision = 18;
    public const int QuantityScale = 4;

    /// <summary>Amounts in KM: numeric(18,2).</summary>
    public const int ValuePrecision = 18;
    public const int ValueScale = 2;
}
