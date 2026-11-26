using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Warehouse.Domain.StockDocuments;

namespace Erp.Seeder.Data;

/// <summary>Everything one seeding run creates, generated up front so it can be checked without a database.</summary>
internal sealed record SeedData(
    CompanySeed Company,
    UserSeed Administrator,
    IReadOnlyList<RoleSeed> Roles,
    IReadOnlyList<UnitSeed> Units,
    IReadOnlyList<PartnerSeed> Partners,
    IReadOnlyList<ArticleSeed> Articles,
    IReadOnlyList<WarehouseSeed> Warehouses,
    IReadOnlyList<NumberSeriesSeed> NumberSeries,
    IReadOnlyList<ReceiptSeed> OpeningReceipts,
    IReadOnlyList<VatRateSeed> VatRates,
    IReadOnlyList<PurchasingNumberSeriesSeed> PurchasingNumberSeries,
    IReadOnlyList<PurchaseOrderSeed> PurchaseOrders);

internal sealed record CompanySeed(string Name, string TaxId, string VatNumber, string Address, string PostalCode, string City);

internal sealed record UserSeed(string Email, string DisplayName);

/// <summary>A role and the one user who holds it.</summary>
internal sealed record RoleSeed(string Name, IReadOnlyList<string> Permissions, UserSeed User);

internal sealed record UnitSeed(string Code, string Name);

internal sealed record PartnerSeed(
    string Code,
    string Name,
    string TaxId,
    string? VatNumber,
    string Address,
    string PostalCode,
    string City,
    bool IsCustomer,
    bool IsSupplier,
    bool IsActive);

internal sealed record ArticleSeed(string Code, string Name, ArticleType Type, TrackingType Tracking, string UnitCode, bool IsActive, string? DefaultVatCode = null);

internal sealed record WarehouseSeed(string Code, string Name);

internal sealed record NumberSeriesSeed(StockDocumentType DocumentType, DateOnly ValidFrom, string Prefix, int Padding);

/// <summary>Opening stock of one warehouse (part of it, at most <c>SeedDataGenerator.MaxReceiptLines</c> lines per receipt).</summary>
internal sealed record ReceiptSeed(string WarehouseCode, DateOnly DocumentDate, IReadOnlyList<ReceiptLineSeed> Lines);

internal sealed record ReceiptLineSeed(
    string ArticleCode,
    decimal Quantity,
    decimal UnitCost,
    string? LotNumber,
    DateOnly? LotExpiryDate,
    IReadOnlyList<string> SerialNumbers)
{
    /// <summary>What the line adds to the stock value: quantity × unit cost in fening, as the Warehouse module rounds it.</summary>
    public decimal Value => Math.Round(Quantity * UnitCost, 2, MidpointRounding.AwayFromZero);
}

internal sealed record VatRateSeed(string Code, string Name, decimal Rate, DateOnly ValidFrom);

internal sealed record PurchasingNumberSeriesSeed(PurchasingDocumentType DocumentType, DateOnly ValidFrom, string Prefix, int Padding);

/// <summary>
/// A confirmed purchase order to a supplier, into the first warehouse. <see cref="ReceivedShare"/> of each line is
/// received in one goods receipt (0: nothing yet, 0.5: partly, 1: fully); a fully received order with
/// <see cref="SupplierInvoiceNumber"/> is invoiced at the order price × <see cref="InvoicePriceFactor"/> on its first line.
/// </summary>
internal sealed record PurchaseOrderSeed(
    string SupplierCode,
    DateOnly OrderDate,
    IReadOnlyList<PurchaseOrderLineSeed> Lines,
    decimal ReceivedShare,
    string? SupplierInvoiceNumber,
    decimal InvoicePriceFactor)
{
    public DateOnly ReceiptDate => OrderDate.AddDays(3);

    public DateOnly InvoiceDate => OrderDate.AddDays(5);
}

internal sealed record PurchaseOrderLineSeed(string ArticleCode, decimal Quantity, decimal UnitPrice, string? LotNumber)
{
    /// <summary>Whole pieces stay whole: the received part is rounded down to the order's decimals.</summary>
    public decimal ReceivedQuantity(decimal share) => share == 1m ? Quantity : Math.Floor(Quantity * share);

    public decimal ReceivedValue(decimal share) => Math.Round(ReceivedQuantity(share) * UnitPrice, 2, MidpointRounding.AwayFromZero);
}
