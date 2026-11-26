namespace Erp.Modules.Purchasing.Contracts.Receipts;

/// <summary>
/// Body of create and update requests for a draft goods receipt against one confirmed order. The unit cost of each line
/// is the order line's price; LOT and serial numbers as the article's tracking type requires.
/// </summary>
public sealed record GoodsReceiptRequest(
    Guid PurchaseOrderId,
    Guid WarehouseId,
    DateOnly ReceiptDate,
    string? Note,
    IReadOnlyList<GoodsReceiptLineRequest> Lines);

public sealed record GoodsReceiptLineRequest(
    Guid OrderLineId,
    decimal Quantity,
    string? LotNumber,
    DateOnly? LotExpiryDate,
    IReadOnlyList<string>? SerialNumbers);

/// <summary><see cref="StockDocumentId"/> is the warehouse receipt the confirmation created.</summary>
public sealed record GoodsReceiptResponse(
    Guid Id,
    PurchaseDocumentState Status,
    string? Number,
    Guid PurchaseOrderId,
    string? OrderNumber,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    DateOnly ReceiptDate,
    string? Note,
    Guid? StockDocumentId,
    string? StockDocumentNumber,
    Guid? ReversesReceiptId,
    Guid? ReversedByReceiptId,
    DateTimeOffset? ConfirmedAt,
    decimal? TotalValue,
    IReadOnlyList<GoodsReceiptLineResponse> Lines);

public sealed record GoodsReceiptLineResponse(
    Guid Id,
    int LineNumber,
    Guid OrderLineId,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    decimal Quantity,
    decimal UnitCost,
    decimal? Value,
    decimal InvoicedQuantity,
    string? LotNumber,
    DateOnly? LotExpiryDate,
    IReadOnlyList<string> SerialNumbers);

public sealed record GoodsReceiptSummaryResponse(
    Guid Id,
    PurchaseDocumentState Status,
    string? Number,
    Guid PurchaseOrderId,
    string? OrderNumber,
    string SupplierName,
    Guid WarehouseId,
    DateOnly ReceiptDate,
    Guid? ReversesReceiptId,
    Guid? ReversedByReceiptId,
    decimal? TotalValue);
