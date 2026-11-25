namespace Erp.Modules.Warehouse.Contracts.StockDocuments;

/// <summary>Receipt: goods come into the target warehouse. Issue: goods leave the source warehouse. Transfer: source to target.</summary>
public enum StockDocumentKind
{
    Receipt,
    Issue,
    Transfer,
}

public enum StockDocumentState
{
    Draft,
    Confirmed,
}

/// <summary>
/// Body of create and update requests for a draft. A receipt names only the target warehouse, an issue
/// only the source, a transfer both.
/// </summary>
public sealed record StockDocumentRequest(
    StockDocumentKind Type,
    DateOnly DocumentDate,
    Guid? SourceWarehouseId,
    Guid? TargetWarehouseId,
    string? Note,
    IReadOnlyList<StockDocumentLineRequest> Lines);

/// <summary>
/// <see cref="UnitCost"/> (KM) is required on receipts and not allowed otherwise. A LOT tracked article needs
/// <see cref="LotNumber"/>; a serial tracked article needs one serial number per piece.
/// </summary>
public sealed record StockDocumentLineRequest(
    Guid ArticleId,
    decimal Quantity,
    decimal? UnitCost,
    string? LotNumber,
    DateOnly? LotExpiryDate,
    IReadOnlyList<string>? SerialNumbers);

public sealed record StockDocumentResponse(
    Guid Id,
    StockDocumentKind Type,
    StockDocumentState Status,
    string? Number,
    DateOnly DocumentDate,
    Guid? SourceWarehouseId,
    Guid? TargetWarehouseId,
    string? Note,
    Guid? ReversesDocumentId,
    Guid? ReversedByDocumentId,
    DateTimeOffset? ConfirmedAt,
    decimal? TotalValue,
    IReadOnlyList<StockDocumentLineResponse> Lines);

/// <summary><see cref="Value"/> (KM) is known once the document is confirmed.</summary>
public sealed record StockDocumentLineResponse(
    Guid Id,
    int LineNumber,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    decimal Quantity,
    decimal? UnitCost,
    decimal? Value,
    string? LotNumber,
    DateOnly? LotExpiryDate,
    IReadOnlyList<string> SerialNumbers);

public sealed record StockDocumentSummaryResponse(
    Guid Id,
    StockDocumentKind Type,
    StockDocumentState Status,
    string? Number,
    DateOnly DocumentDate,
    Guid? SourceWarehouseId,
    Guid? TargetWarehouseId,
    Guid? ReversesDocumentId,
    Guid? ReversedByDocumentId,
    int LineCount,
    decimal? TotalValue);

/// <summary>
/// Listed (as <c>shortages</c> in the 409 problem with code <c>Stock.Insufficient</c>) when a document would take
/// more than is in stock. For a serial number, <see cref="Requested"/> is 1 and <see cref="Available"/> is 0.
/// </summary>
public sealed record StockShortageResponse(
    Guid WarehouseId,
    Guid ArticleId,
    string ArticleCode,
    string? LotNumber,
    string? SerialNumber,
    decimal Requested,
    decimal Available);

/// <summary>
/// The 409 problem of confirm and storno, documented so clients get a typed <see cref="Shortages"/> list.
/// <see cref="Code"/> is <c>Stock.Insufficient</c> (with shortages) or another conflict, e.g. <c>StockDocument.NotDraft</c>.
/// </summary>
public sealed record StockProblemResponse(
    string? Type,
    string? Title,
    int? Status,
    string Code,
    IReadOnlyList<StockShortageResponse>? Shortages);
