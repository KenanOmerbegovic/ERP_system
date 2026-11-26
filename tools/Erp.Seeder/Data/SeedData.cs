using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Sales.Domain.Numbering;
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
    IReadOnlyList<PurchaseOrderSeed> PurchaseOrders,
    IReadOnlyList<SalesNumberSeriesSeed> SalesNumberSeries,
    IReadOnlyList<OfferSeed> Offers,
    IReadOnlyList<SalesOrderSeed> SalesOrders);

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

internal sealed record SalesNumberSeriesSeed(SalesDocumentType DocumentType, DateOnly ValidFrom, string Prefix, int Padding);

/// <summary>A goods or service line of an offer or sales order; goods go out of the first warehouse.</summary>
internal sealed record SalesLineSeed(string ArticleCode, bool IsService, decimal Quantity, decimal UnitPrice);

internal enum OfferOutcome
{
    /// <summary>Sent and waiting for the customer.</summary>
    Sent,
    Rejected,
}

/// <summary>An offer that did not become an order (accepted offers are part of their <see cref="SalesOrderSeed"/>).</summary>
internal sealed record OfferSeed(string CustomerCode, DateOnly OfferDate, IReadOnlyList<SalesLineSeed> Lines, OfferOutcome Outcome);

internal enum SalesCorrection
{
    None,

    /// <summary>A credit note returns part of the first goods line to stock.</summary>
    Return,

    /// <summary>A credit note takes 10 % off the first line's value; no stock moves.</summary>
    ValueReduction,

    /// <summary>The whole invoice is reversed.</summary>
    InvoiceStorno,
}

/// <summary>
/// A confirmed sales order (from an accepted offer when <see cref="FromOffer"/>). <see cref="DeliveredShare"/> of each
/// goods line goes out in one delivery note (0, 0.5 or 1); when <see cref="Invoiced"/>, what was delivered and all
/// services are invoiced, then corrected as <see cref="Correction"/> says.
/// </summary>
internal sealed record SalesOrderSeed(
    string CustomerCode,
    DateOnly OrderDate,
    IReadOnlyList<SalesLineSeed> Lines,
    bool FromOffer,
    decimal DeliveredShare,
    bool Invoiced,
    SalesCorrection Correction)
{
    public DateOnly OfferDate => OrderDate.AddDays(-3);

    public DateOnly DeliveryDate => OrderDate.AddDays(2);

    public DateOnly InvoiceDate => OrderDate.AddDays(4);

    public DateOnly CreditNoteDate => OrderDate.AddDays(10);

    /// <summary>The delivered part of a goods line, rounded down: to whole pieces for whole quantities, else to two decimals.</summary>
    public static decimal Delivered(SalesLineSeed line, decimal share) =>
        line.IsService ? 0m
        : share == 1m ? line.Quantity
        : line.Quantity == decimal.Truncate(line.Quantity) ? Math.Floor(line.Quantity * share)
        : Math.Floor(line.Quantity * share * 100m) / 100m;
}
