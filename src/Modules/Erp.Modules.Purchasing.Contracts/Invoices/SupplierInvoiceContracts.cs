namespace Erp.Modules.Purchasing.Contracts.Invoices;

/// <summary>
/// Body of create and update requests for a draft supplier invoice against one order. Each line invoices (part of) a
/// confirmed receipt line; prices are KM per unit without PDV.
/// </summary>
public sealed record SupplierInvoiceRequest(
    Guid PurchaseOrderId,
    string SupplierInvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string? Note,
    IReadOnlyList<SupplierInvoiceLineRequest> Lines);

/// <summary><see cref="VatCode"/> defaults to the order line's VAT code.</summary>
public sealed record SupplierInvoiceLineRequest(Guid ReceiptLineId, decimal Quantity, decimal UnitPrice, string? VatCode);

/// <summary>Amounts in KM. PDV is computed by the country's rules for the invoice date.</summary>
public sealed record SupplierInvoiceResponse(
    Guid Id,
    PurchaseDocumentState Status,
    string? Number,
    Guid PurchaseOrderId,
    string? OrderNumber,
    Guid SupplierId,
    string SupplierName,
    string SupplierInvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string? Note,
    Guid? ReversesInvoiceId,
    Guid? ReversedByInvoiceId,
    DateTimeOffset? ConfirmedAt,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrossAmount,
    decimal PriceDifference,
    IReadOnlyList<SupplierInvoiceVatTotalResponse> VatTotals,
    IReadOnlyList<SupplierInvoiceLineResponse> Lines);

/// <summary>
/// <see cref="PriceDifference"/> = <see cref="NetAmount"/> − <see cref="ReceivedValue"/>: what the invoice charges above
/// (or below) the cost the goods entered stock at. Reported to Finance; stock is not revalued.
/// </summary>
public sealed record SupplierInvoiceLineResponse(
    Guid Id,
    int LineNumber,
    Guid ReceiptId,
    Guid ReceiptLineId,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    decimal Quantity,
    decimal UnitPrice,
    decimal ReceivedUnitCost,
    string VatCode,
    decimal NetAmount,
    decimal VatAmount,
    decimal ReceivedValue,
    decimal PriceDifference);

public sealed record SupplierInvoiceVatTotalResponse(string VatCode, decimal Rate, decimal Base, decimal VatAmount);

public sealed record SupplierInvoiceSummaryResponse(
    Guid Id,
    PurchaseDocumentState Status,
    string? Number,
    string SupplierInvoiceNumber,
    Guid PurchaseOrderId,
    string SupplierName,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    Guid? ReversesInvoiceId,
    Guid? ReversedByInvoiceId,
    decimal GrossAmount);

/// <summary>A confirmed receipt line with what is still to be invoiced, for building an invoice.</summary>
public sealed record InvoiceableReceiptLineResponse(
    Guid ReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    Guid ReceiptLineId,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    decimal ReceivedQuantity,
    decimal InvoicedQuantity,
    decimal UnitCost,
    string VatCode);
