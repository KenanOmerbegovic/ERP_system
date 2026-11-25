using Erp.SharedKernel.Messaging;

namespace Erp.Modules.Warehouse.Contracts;

/// <summary>
/// One line of a confirmed stock document. For a storno (<c>ReversesDocumentId</c> set) quantity and value are
/// negative (red storno). <see cref="Value"/> is the document value in KM. <see cref="PriceDifference"/> is the
/// part of it that did not change the stock value; it is non-zero only for a storno that would otherwise leave
/// stock with a negative value or with value but no quantity.
/// </summary>
public sealed record StockEventLine(
    Guid ArticleId,
    string? LotNumber,
    IReadOnlyList<string> SerialNumbers,
    decimal Quantity,
    decimal Value,
    decimal PriceDifference);

/// <summary>A receipt was confirmed: goods came into <see cref="WarehouseId"/> at their unit cost.</summary>
public sealed record StockReceived(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid DocumentId,
    string DocumentNumber,
    DateOnly DocumentDate,
    Guid WarehouseId,
    Guid? ReversesDocumentId,
    IReadOnlyList<StockEventLine> Lines) : IIntegrationEvent;

/// <summary>An issue was confirmed: goods left <see cref="WarehouseId"/> at their weighted-average cost.</summary>
public sealed record StockIssued(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid DocumentId,
    string DocumentNumber,
    DateOnly DocumentDate,
    Guid WarehouseId,
    Guid? ReversesDocumentId,
    IReadOnlyList<StockEventLine> Lines) : IIntegrationEvent;

/// <summary>A transfer was confirmed: goods moved at the source warehouse's weighted-average cost.</summary>
public sealed record StockTransferred(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid CompanyId,
    Guid DocumentId,
    string DocumentNumber,
    DateOnly DocumentDate,
    Guid SourceWarehouseId,
    Guid TargetWarehouseId,
    Guid? ReversesDocumentId,
    IReadOnlyList<StockEventLine> Lines) : IIntegrationEvent;
