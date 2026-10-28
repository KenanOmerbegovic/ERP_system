namespace Erp.Modules.Core.Domain;

/// <summary>Maximum text lengths, shared by validation and the database schema.</summary>
public static class FieldLengths
{
    public const int Code = 50;
    public const int Name = 200;
    public const int TaxId = 20;
    public const int VatNumber = 20;
    public const int Address = 200;
    public const int PostalCode = 20;
    public const int City = 100;
    public const int CountryCode = 2;
    public const int UnitCode = 10;
    public const int UnitName = 50;
}
