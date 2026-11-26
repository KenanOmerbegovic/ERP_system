using Erp.Modules.Core.Contracts.Articles;

namespace Erp.Modules.Purchasing.Contracts.Orders;

/// <summary>Body of create and update requests for a draft order. Prices are KM per unit, without PDV.</summary>
public sealed record PurchaseOrderRequest(
    Guid SupplierId,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid? WarehouseId,
    string? Note,
    IReadOnlyList<PurchaseOrderLineRequest> Lines);

/// <summary><see cref="VatCode"/> defaults to the article's default VAT code.</summary>
public sealed record PurchaseOrderLineRequest(Guid ArticleId, decimal Quantity, decimal UnitPrice, string? VatCode);

public sealed record PurchaseOrderResponse(
    Guid Id,
    PurchaseDocumentState Status,
    string? Number,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid? WarehouseId,
    string? Note,
    Guid? ReversesOrderId,
    Guid? ReversedByOrderId,
    DateTimeOffset? ConfirmedAt,
    decimal NetAmount,
    IReadOnlyList<PurchaseOrderLineResponse> Lines);

/// <summary><see cref="OpenQuantity"/> is ordered minus received: what receipts may still bring in.</summary>
public sealed record PurchaseOrderLineResponse(
    Guid Id,
    int LineNumber,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    ArticleTracking Tracking,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    decimal NetAmount,
    decimal ReceivedQuantity,
    decimal InvoicedQuantity,
    decimal OpenQuantity);

public sealed record PurchaseOrderSummaryResponse(
    Guid Id,
    PurchaseDocumentState Status,
    string? Number,
    Guid SupplierId,
    string SupplierName,
    DateOnly OrderDate,
    Guid? ReversesOrderId,
    Guid? ReversedByOrderId,
    decimal NetAmount,
    bool FullyReceived);
