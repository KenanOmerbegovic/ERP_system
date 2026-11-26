using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;

namespace Erp.Modules.Warehouse.Contracts.Commands;

/// <summary>
/// Identifies the caller's document a command belongs to, e.g. ("purchasing", goods receipt id). Sending the same key
/// again returns the first result and moves stock only once.
/// </summary>
public sealed record IdempotencyKey(string SourceModule, Guid SourceDocumentId);

/// <summary>Receive goods into <see cref="WarehouseId"/>; the stock document is created and confirmed in one step.</summary>
public sealed record StockReceiptCommand(
    IdempotencyKey Key,
    CompanyId CompanyId,
    Guid WarehouseId,
    DateOnly DocumentDate,
    string? Note,
    IReadOnlyList<StockReceiptCommandLine> Lines);

/// <summary><see cref="UnitCost"/> is KM per unit; LOT and serial numbers as the article's tracking type requires.</summary>
public sealed record StockReceiptCommandLine(
    Guid ArticleId,
    decimal Quantity,
    decimal UnitCost,
    string? LotNumber,
    DateOnly? LotExpiryDate,
    IReadOnlyList<string> SerialNumbers);

/// <summary>Reverse (storno) the stock document an earlier command with <see cref="OriginalKey"/> created.</summary>
public sealed record StockReversalCommand(IdempotencyKey Key, CompanyId CompanyId, IdempotencyKey OriginalKey);

/// <summary>The confirmed stock document; <see cref="LineValues"/> are the KM values of its lines, in the order sent.</summary>
public sealed record StockDocumentReference(Guid DocumentId, string Number, decimal TotalValue, IReadOnlyList<decimal> LineValues);

/// <summary>
/// Synchronous, idempotent stock commands for other modules (CLAUDE.md: commands that must be able to fail immediately).
/// Failures are the Warehouse module's errors: <c>Stock.Insufficient</c> (an <see cref="StockDocuments.InsufficientStockError"/>
/// conflict with shortages), validation errors per line, <c>SerialNumber.AlreadyInStock</c>,
/// <c>NumberSeries.NotConfigured</c>, or <see cref="StockCommandErrors.IdempotencyKeyReusedCode"/>. A failed command
/// leaves nothing behind, so it can be corrected and sent again with the same key.
/// </summary>
public interface IStockDocumentCommands
{
    Task<Result<StockDocumentReference>> ReceiveAsync(StockReceiptCommand command, CancellationToken cancellationToken);

    Task<Result<StockDocumentReference>> ReverseAsync(StockReversalCommand command, CancellationToken cancellationToken);
}

public static class StockCommandErrors
{
    public const string IdempotencyKeyReusedCode = "Command.IdempotencyKeyReused";
    public const string OriginalNotFoundCode = "Command.OriginalNotFound";

    public static readonly Error IdempotencyKeyReused =
        Error.Conflict(IdempotencyKeyReusedCode, "This idempotency key was already used for a different command.");

    public static readonly Error OriginalNotFound =
        Error.NotFound(OriginalNotFoundCode, "No stock document was created for the original key.");
}
