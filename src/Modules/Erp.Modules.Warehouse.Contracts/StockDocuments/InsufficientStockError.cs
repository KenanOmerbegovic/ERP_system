using Erp.SharedKernel.Results;

namespace Erp.Modules.Warehouse.Contracts.StockDocuments;

/// <summary>
/// Stock.Insufficient (409): a document would take more than is in stock; nothing was saved. In Contracts so that
/// modules calling the Warehouse command contract can pass the shortages on to their clients.
/// </summary>
public sealed record InsufficientStockError(IReadOnlyList<StockShortageResponse> Shortages)
    : Error(ErrorCode, "Not enough stock.", ErrorType.Conflict)
{
    public const string ErrorCode = "Stock.Insufficient";
}
