namespace Erp.Modules.Core.Contracts;

/// <summary>Permissions the Core module checks. Companies combine them into roles.</summary>
public static class CorePermissions
{
    public const string CompaniesRead = "core.companies.read";
    public const string CompaniesWrite = "core.companies.write";
    public const string BusinessPartnersRead = "core.business-partners.read";
    public const string BusinessPartnersWrite = "core.business-partners.write";
    public const string UnitsOfMeasureRead = "core.units-of-measure.read";
    public const string UnitsOfMeasureWrite = "core.units-of-measure.write";
    public const string ArticlesRead = "core.articles.read";
    public const string ArticlesWrite = "core.articles.write";

    public static IReadOnlyList<string> All { get; } =
    [
        CompaniesRead,
        CompaniesWrite,
        BusinessPartnersRead,
        BusinessPartnersWrite,
        UnitsOfMeasureRead,
        UnitsOfMeasureWrite,
        ArticlesRead,
        ArticlesWrite,
    ];
}
