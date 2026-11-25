namespace Erp.Modules.Warehouse.Contracts.Stock;

/// <summary>
/// Stock of one article in one warehouse. <see cref="AverageCost"/> is the weighted average (KM per unit),
/// <see cref="Value"/> the stock value in KM. <see cref="Lots"/> splits the quantity of a LOT tracked article.
/// </summary>
public sealed record StockBalanceResponse(
    Guid WarehouseId,
    string WarehouseCode,
    Guid ArticleId,
    string ArticleCode,
    string ArticleName,
    StockTrackingKind Tracking,
    decimal Quantity,
    decimal AverageCost,
    decimal Value,
    IReadOnlyList<StockLotResponse> Lots);

public sealed record StockLotResponse(string LotNumber, DateOnly? ExpiryDate, decimal Quantity);

public sealed record SerialNumberResponse(
    Guid ArticleId,
    string ArticleCode,
    string SerialNumber,
    bool InStock,
    Guid? WarehouseId);

public enum StockTrackingKind
{
    None,
    Lot,
    Serial,
}
