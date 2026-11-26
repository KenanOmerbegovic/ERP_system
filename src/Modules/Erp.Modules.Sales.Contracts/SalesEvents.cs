using Erp.SharedKernel.Messaging;

namespace Erp.Modules.Sales.Contracts;

// For Finance (Phase 3), see docs/posting-rules.md. A storno publishes the same event with the reversed document's id in
// Reverses…Id and negative quantities and amounts (red storno). Amounts are KM.

public sealed record SalesOrderEventLine(Guid ArticleId, bool IsService, decimal Quantity, decimal UnitPrice, string VatCode, decimal NetAmount);

/// <summary>A sales order was confirmed (or, with <see cref="ReversesOrderId"/>, reversed).</summary>
public sealed record SalesOrderConfirmed(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid OrderId,
    string Number,
    Guid CustomerId,
    DateOnly OrderDate,
    Guid? OfferId,
    Guid? ReversesOrderId,
    decimal NetAmount,
    IReadOnlyList<SalesOrderEventLine> Lines) : IIntegrationEvent;

/// <summary><see cref="CostValue"/> is what the goods left stock at (weighted average): the cost of goods sold.</summary>
public sealed record GoodsDeliveredLine(Guid ArticleId, Guid OrderLineId, Guid DeliveryLineId, decimal Quantity, decimal CostValue);

/// <summary>
/// Goods left the warehouse to a customer (warehouse issue <see cref="StockDocumentId"/>). Finance books the cost of
/// goods sold against stock: <see cref="CostValue"/> in total and per line.
/// </summary>
public sealed record GoodsDelivered(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid DeliveryNoteId,
    string Number,
    Guid OrderId,
    Guid CustomerId,
    Guid WarehouseId,
    DateOnly DeliveryDate,
    Guid StockDocumentId,
    Guid? ReversesDeliveryNoteId,
    decimal CostValue,
    IReadOnlyList<GoodsDeliveredLine> Lines) : IIntegrationEvent;

public sealed record SalesVatTotal(string VatCode, decimal Rate, decimal Base, decimal VatAmount);

/// <summary>
/// A goods line names the delivery line it invoices; a service line has none (services are invoiced from the order).
/// </summary>
public sealed record InvoiceIssuedLine(
    Guid InvoiceLineId,
    Guid ArticleId,
    bool IsService,
    Guid OrderLineId,
    Guid? DeliveryNoteId,
    Guid? DeliveryLineId,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    decimal NetAmount,
    decimal VatAmount);

/// <summary>An invoice (output PDV) was issued: a receivable of <see cref="GrossAmount"/> from the customer, revenue of <see cref="NetAmount"/>.</summary>
public sealed record InvoiceIssued(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid InvoiceId,
    string Number,
    Guid CustomerId,
    Guid OrderId,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    Guid? ReversesInvoiceId,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrossAmount,
    IReadOnlyList<SalesVatTotal> VatTotals,
    IReadOnlyList<InvoiceIssuedLine> Lines) : IIntegrationEvent;

/// <summary><see cref="ReturnedCostValue"/> is what returned goods re-entered stock at (0 for a value reduction).</summary>
public sealed record CreditNoteIssuedLine(
    Guid ArticleId,
    Guid InvoiceLineId,
    CreditNoteLineKind Kind,
    decimal Quantity,
    string VatCode,
    decimal NetAmount,
    decimal VatAmount,
    decimal ReturnedCostValue);

/// <summary>
/// A credit note reduced an invoice: the receivable, revenue and output PDV go down by its amounts. Returned goods came
/// back into <see cref="WarehouseId"/> (warehouse receipt <see cref="StockDocumentId"/>) at their original cost, which
/// reverses that part of the cost of goods sold.
/// </summary>
public sealed record CreditNoteIssued(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid CreditNoteId,
    string Number,
    Guid InvoiceId,
    Guid CustomerId,
    Guid OrderId,
    DateOnly CreditNoteDate,
    Guid? WarehouseId,
    Guid? StockDocumentId,
    Guid? ReversesCreditNoteId,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrossAmount,
    decimal ReturnedCostValue,
    IReadOnlyList<SalesVatTotal> VatTotals,
    IReadOnlyList<CreditNoteIssuedLine> Lines) : IIntegrationEvent;
