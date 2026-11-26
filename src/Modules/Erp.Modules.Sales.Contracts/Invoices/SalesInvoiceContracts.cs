using Erp.Modules.Core.Contracts.Articles;

namespace Erp.Modules.Sales.Contracts.Invoices;

/// <summary>
/// Body of create and update requests for a draft invoice against one confirmed sales order (FR-SAL-03). A goods line
/// invoices (part of) a confirmed delivery line (<see cref="SalesInvoiceLineRequest.DeliveryLineId"/>); a service line
/// invoices (part of) a service order line (<see cref="SalesInvoiceLineRequest.OrderLineId"/>) without a delivery.
/// Prices and VAT codes come from the order line.
/// </summary>
public sealed record SalesInvoiceRequest(
    Guid SalesOrderId,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string? Note,
    IReadOnlyList<SalesInvoiceLineRequest> Lines);

/// <summary>Exactly one of <see cref="DeliveryLineId"/> (goods) and <see cref="OrderLineId"/> (services).</summary>
public sealed record SalesInvoiceLineRequest(Guid? DeliveryLineId, Guid? OrderLineId, decimal Quantity);

/// <summary>Amounts in KM. PDV is computed by the country's rules for the invoice date (FR-SAL-04).</summary>
public sealed record SalesInvoiceResponse(
    Guid Id,
    SalesDocumentState Status,
    string? Number,
    Guid SalesOrderId,
    string? OrderNumber,
    Guid CustomerId,
    string CustomerName,
    string? CustomerTaxId,
    string? CustomerVatNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string? Note,
    Guid? ReversesInvoiceId,
    Guid? ReversedByInvoiceId,
    DateTimeOffset? ConfirmedAt,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrossAmount,
    decimal CreditedAmount,
    IReadOnlyList<SalesInvoiceVatTotalResponse> VatTotals,
    IReadOnlyList<SalesInvoiceLineResponse> Lines);

/// <summary>
/// <see cref="CreditedAmount"/> is the net amount confirmed credit notes took off this line; <see cref="ReturnedQuantity"/>
/// the quantity they returned to stock.
/// </summary>
public sealed record SalesInvoiceLineResponse(
    Guid Id,
    int LineNumber,
    Guid OrderLineId,
    Guid? DeliveryNoteId,
    Guid? DeliveryLineId,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    ArticleKind Kind,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    decimal VatRate,
    decimal NetAmount,
    decimal VatAmount,
    decimal CreditedAmount,
    decimal ReturnedQuantity);

public sealed record SalesInvoiceVatTotalResponse(string VatCode, decimal Rate, decimal Base, decimal VatAmount);

public sealed record SalesInvoiceSummaryResponse(
    Guid Id,
    SalesDocumentState Status,
    string? Number,
    Guid SalesOrderId,
    string? OrderNumber,
    string CustomerName,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    Guid? ReversesInvoiceId,
    Guid? ReversedByInvoiceId,
    decimal GrossAmount);

/// <summary>
/// What an invoice of the order can still take: confirmed delivery lines with uninvoiced quantity (goods) and service
/// order lines with uninvoiced quantity. <see cref="DeliveryLineId"/> is null for a service.
/// </summary>
public sealed record InvoiceableLineResponse(
    Guid OrderLineId,
    Guid? DeliveryNoteId,
    string? DeliveryNoteNumber,
    DateOnly? DeliveryDate,
    Guid? DeliveryLineId,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    ArticleKind Kind,
    decimal Quantity,
    decimal InvoicedQuantity,
    decimal UnitPrice,
    string VatCode);
