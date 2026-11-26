using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Sales.Contracts.Invoices;

namespace Erp.Modules.Sales.Contracts.CreditNotes;

/// <summary>
/// Body of create and update requests for a draft credit note against one confirmed invoice (FR-SAL-05).
/// <see cref="WarehouseId"/> is required when a line returns goods: they come back into it at their original cost.
/// </summary>
public sealed record CreditNoteRequest(
    Guid InvoiceId,
    DateOnly CreditNoteDate,
    Guid? WarehouseId,
    string? Note,
    IReadOnlyList<CreditNoteLineRequest> Lines);

/// <summary>
/// Return: <see cref="Quantity"/> pieces come back at the invoice price (serial numbers as delivered, for serial-tracked
/// goods). ValueReduction: <see cref="NetAmount"/> KM off the line, no stock. A line never credits more than the invoice
/// line's net amount still open.
/// </summary>
public sealed record CreditNoteLineRequest(
    Guid InvoiceLineId,
    CreditNoteLineKind Kind,
    decimal? Quantity,
    decimal? NetAmount,
    IReadOnlyList<string>? SerialNumbers);

/// <summary>PDV is computed at the rates of the invoice's date, as it corrects that invoice.</summary>
public sealed record CreditNoteResponse(
    Guid Id,
    SalesDocumentState Status,
    string? Number,
    Guid InvoiceId,
    string? InvoiceNumber,
    Guid SalesOrderId,
    Guid CustomerId,
    string CustomerName,
    DateOnly CreditNoteDate,
    Guid? WarehouseId,
    string? Note,
    Guid? StockDocumentId,
    string? StockDocumentNumber,
    Guid? ReversesCreditNoteId,
    Guid? ReversedByCreditNoteId,
    DateTimeOffset? ConfirmedAt,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrossAmount,
    decimal? ReturnedCostValue,
    IReadOnlyList<SalesInvoiceVatTotalResponse> VatTotals,
    IReadOnlyList<CreditNoteLineResponse> Lines);

public sealed record CreditNoteLineResponse(
    Guid Id,
    int LineNumber,
    Guid InvoiceLineId,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    ArticleKind ArticleKind,
    CreditNoteLineKind Kind,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    decimal VatRate,
    decimal NetAmount,
    decimal VatAmount,
    string? LotNumber,
    IReadOnlyList<string> SerialNumbers,
    decimal? ReturnedCostValue);

public sealed record CreditNoteSummaryResponse(
    Guid Id,
    SalesDocumentState Status,
    string? Number,
    Guid InvoiceId,
    string? InvoiceNumber,
    string CustomerName,
    DateOnly CreditNoteDate,
    Guid? ReversesCreditNoteId,
    Guid? ReversedByCreditNoteId,
    decimal GrossAmount);
