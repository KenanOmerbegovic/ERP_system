namespace Erp.Modules.Sales.Contracts.Deliveries;

/// <summary>
/// Body of create and update requests for a draft delivery note against one confirmed sales order (goods lines only;
/// part of the order is allowed). LOT and serial numbers as the article's tracking type requires.
/// </summary>
public sealed record DeliveryNoteRequest(
    Guid SalesOrderId,
    Guid WarehouseId,
    DateOnly DeliveryDate,
    string? Note,
    IReadOnlyList<DeliveryNoteLineRequest> Lines);

public sealed record DeliveryNoteLineRequest(Guid OrderLineId, decimal Quantity, string? LotNumber, IReadOnlyList<string>? SerialNumbers);

/// <summary>
/// <see cref="StockDocumentId"/> is the warehouse issue the confirmation created; <see cref="CostValue"/> is the cost of
/// the goods it took out (weighted average), once confirmed.
/// </summary>
public sealed record DeliveryNoteResponse(
    Guid Id,
    SalesDocumentState Status,
    string? Number,
    Guid SalesOrderId,
    string? OrderNumber,
    Guid CustomerId,
    string CustomerName,
    Guid WarehouseId,
    DateOnly DeliveryDate,
    string? Note,
    Guid? StockDocumentId,
    string? StockDocumentNumber,
    Guid? ReversesDeliveryNoteId,
    Guid? ReversedByDeliveryNoteId,
    DateTimeOffset? ConfirmedAt,
    decimal? CostValue,
    IReadOnlyList<DeliveryNoteLineResponse> Lines);

public sealed record DeliveryNoteLineResponse(
    Guid Id,
    int LineNumber,
    Guid OrderLineId,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    decimal Quantity,
    decimal? CostValue,
    decimal InvoicedQuantity,
    string? LotNumber,
    IReadOnlyList<string> SerialNumbers);

public sealed record DeliveryNoteSummaryResponse(
    Guid Id,
    SalesDocumentState Status,
    string? Number,
    Guid SalesOrderId,
    string? OrderNumber,
    string CustomerName,
    Guid WarehouseId,
    DateOnly DeliveryDate,
    Guid? ReversesDeliveryNoteId,
    Guid? ReversedByDeliveryNoteId,
    decimal? CostValue);
