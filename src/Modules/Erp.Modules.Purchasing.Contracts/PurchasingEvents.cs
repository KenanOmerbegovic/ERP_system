using Erp.SharedKernel.Messaging;

namespace Erp.Modules.Purchasing.Contracts;

// For Finance (Phase 3). A storno publishes the same event with the reversed document's id in Reverses…Id and
// negative quantities and amounts (red storno). Amounts are KM.

public sealed record PurchaseOrderEventLine(Guid ArticleId, decimal Quantity, decimal UnitPrice, string VatCode, decimal NetAmount);

/// <summary>A purchase order was confirmed (or, with <see cref="ReversesOrderId"/>, reversed).</summary>
public sealed record PurchaseOrderConfirmed(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid OrderId,
    string Number,
    Guid SupplierId,
    DateOnly OrderDate,
    Guid? ReversesOrderId,
    decimal NetAmount,
    IReadOnlyList<PurchaseOrderEventLine> Lines) : IIntegrationEvent;

public sealed record GoodsReceivedLine(Guid ArticleId, Guid OrderLineId, decimal Quantity, decimal UnitCost, decimal Value);

/// <summary>
/// Goods were received against a purchase order and entered stock (warehouse document <see cref="StockDocumentId"/>)
/// at the order price. Finance books stock against goods received not invoiced.
/// </summary>
public sealed record GoodsReceived(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid ReceiptId,
    string Number,
    Guid OrderId,
    Guid SupplierId,
    Guid WarehouseId,
    DateOnly ReceiptDate,
    Guid StockDocumentId,
    Guid? ReversesReceiptId,
    decimal TotalValue,
    IReadOnlyList<GoodsReceivedLine> Lines) : IIntegrationEvent;

public sealed record SupplierInvoiceEventVatTotal(string VatCode, decimal Rate, decimal Base, decimal VatAmount);

/// <summary>
/// <see cref="ReceivedValue"/> is what the goods entered stock at; <see cref="PriceDifference"/> = net − received value,
/// for Finance's price-difference account (stock is not revalued).
/// </summary>
public sealed record SupplierInvoiceEventLine(
    Guid ArticleId,
    Guid ReceiptId,
    Guid ReceiptLineId,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    decimal NetAmount,
    decimal VatAmount,
    decimal ReceivedValue,
    decimal PriceDifference);

/// <summary>A supplier invoice (input PDV) was registered: a payable of <see cref="GrossAmount"/> to the supplier.</summary>
public sealed record SupplierInvoiceRegistered(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid InvoiceId,
    string Number,
    string SupplierInvoiceNumber,
    Guid SupplierId,
    Guid OrderId,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    Guid? ReversesInvoiceId,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrossAmount,
    IReadOnlyList<SupplierInvoiceEventVatTotal> VatTotals,
    IReadOnlyList<SupplierInvoiceEventLine> Lines) : IIntegrationEvent;
