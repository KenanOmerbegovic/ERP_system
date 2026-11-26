using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Sales.Contracts.Offers;

namespace Erp.Modules.Sales.Contracts.Orders;

/// <summary>Body of create and update requests for a draft sales order. Prices are KM per unit, without PDV.</summary>
public sealed record SalesOrderRequest(
    Guid CustomerId,
    DateOnly OrderDate,
    Guid? WarehouseId,
    string? Note,
    IReadOnlyList<SalesLineRequest> Lines);

public sealed record SalesOrderResponse(
    Guid Id,
    SalesDocumentState Status,
    string? Number,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    DateOnly OrderDate,
    Guid? WarehouseId,
    string? Note,
    Guid? OfferId,
    string? OfferNumber,
    Guid? ReversesOrderId,
    Guid? ReversedByOrderId,
    DateTimeOffset? ConfirmedAt,
    decimal NetAmount,
    IReadOnlyList<SalesOrderLineResponse> Lines);

/// <summary>
/// <see cref="OpenQuantity"/> is what delivery notes may still take (goods: ordered − delivered; services: 0).
/// <see cref="InvoiceableQuantity"/> is what invoices may still take: delivered − invoiced for goods, ordered − invoiced
/// for services.
/// </summary>
public sealed record SalesOrderLineResponse(
    Guid Id,
    int LineNumber,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    ArticleKind Kind,
    ArticleTracking Tracking,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    decimal NetAmount,
    decimal DeliveredQuantity,
    decimal InvoicedQuantity,
    decimal OpenQuantity,
    decimal InvoiceableQuantity);

public sealed record SalesOrderSummaryResponse(
    Guid Id,
    SalesDocumentState Status,
    string? Number,
    Guid CustomerId,
    string CustomerName,
    DateOnly OrderDate,
    Guid? ReversesOrderId,
    Guid? ReversedByOrderId,
    decimal NetAmount,
    bool FullyDelivered,
    bool FullyInvoiced);
