using System.Globalization;
using Erp.SharedKernel.Domain;

namespace Erp.Modules.Purchasing.Domain.Numbering;

public enum PurchasingDocumentType
{
    PurchaseOrder,
    GoodsReceipt,
    SupplierInvoice,
}

/// <summary>
/// Configuration (effective-dated): how confirmed documents of one type are numbered from <see cref="ValidFrom"/> on.
/// The counter runs per company, document type and year of the document date, gapless. No defaults in code.
/// Same shape as the Warehouse module's series; each module owns its own configuration.
/// </summary>
public sealed class NumberSeries : Entity<Guid>
{
    public const int MinPadding = 1;
    public const int MaxPadding = 10;

    private NumberSeries(Guid id, CompanyId companyId, PurchasingDocumentType documentType, DateOnly validFrom, string prefix, int padding)
        : base(id, companyId)
    {
        if (!Enum.IsDefined(documentType))
        {
            throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown document type.");
        }

        if (padding is < MinPadding or > MaxPadding)
        {
            throw new ArgumentOutOfRangeException(nameof(padding), padding, null);
        }

        DocumentType = documentType;
        ValidFrom = validFrom;
        Prefix = Text.Required(prefix);
        Padding = padding;
    }

    private NumberSeries()
    {
    }

    public PurchasingDocumentType DocumentType { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    public string Prefix { get; private set; } = null!;

    public int Padding { get; private set; }

    public static NumberSeries Create(CompanyId companyId, PurchasingDocumentType documentType, DateOnly validFrom, string prefix, int padding) =>
        new(Guid.CreateVersion7(), companyId, documentType, validFrom, prefix, padding);

    /// <summary>E.g. prefix "NAR-", year 2026, counter 7, padding 5: "NAR-2026-00007".</summary>
    public string Format(int year, int counter) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}{year}-{counter.ToString(CultureInfo.InvariantCulture).PadLeft(Padding, '0')}");
}
