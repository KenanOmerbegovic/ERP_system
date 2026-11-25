using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.SharedKernel.Results;

namespace Erp.Modules.Warehouse.Application;

public static class WarehouseErrors
{
    public static readonly Error WarehouseNotFound =
        Error.NotFound("Warehouse.NotFound", "Warehouse not found.");

    public static readonly Error DuplicateWarehouseCode =
        Error.Conflict("Warehouse.DuplicateCode", "A warehouse with this code already exists.");

    public static readonly Error StockDocumentNotFound =
        Error.NotFound("StockDocument.NotFound", "Stock document not found.");

    public static readonly Error StockDocumentNotDraft =
        Error.Conflict("StockDocument.NotDraft", "The document is confirmed and cannot be changed; reverse it with a storno.");

    public static readonly Error StockDocumentNotConfirmed =
        Error.Conflict("StockDocument.NotConfirmed", "Only a confirmed document can be reversed.");

    public static readonly Error StockDocumentIsStorno =
        Error.Conflict("StockDocument.IsStorno", "A storno cannot be reversed.");

    public static readonly Error StockDocumentAlreadyReversed =
        Error.Conflict("StockDocument.AlreadyReversed", "The document is already reversed.");

    public static readonly Error NumberSeriesNotConfigured =
        new("NumberSeries.NotConfigured", "No number series is configured for this document type and date.");

    public static readonly Error DuplicateNumberSeries =
        Error.Conflict("NumberSeries.Duplicate", "A number series for this document type already starts on this date.");

    public static Error StockConflict(string code, string message) => Error.Conflict(code, message);
}

/// <summary>Validation codes for fields checked against stored data (warehouses, articles); clients map them to UI text.</summary>
public static class WarehouseValidationCodes
{
    public const string WarehouseNotFound = "Warehouse.NotFound";
    public const string WarehouseInactive = "Warehouse.Inactive";
    public const string ArticleNotFound = "Article.NotFound";
    public const string ArticleNotStockable = "Article.NotStockable";
    public const string DocumentDateInFuture = "DocumentDate.InFuture";
    public const string SameWarehouse = "Transfer.SameWarehouse";
    public const string LotNumberRequired = "StockLine.LotNumberRequired";
    public const string LotNumberNotAllowed = "StockLine.LotNumberNotAllowed";
    public const string SerialNumbersNotAllowed = "StockLine.SerialNumbersNotAllowed";
    public const string SerialNumberCountMismatch = "StockLine.SerialNumberCountMismatch";
    public const string SerialNumberDuplicate = "StockLine.SerialNumberDuplicate";
    public const string NotAllowed = "Field.NotAllowed";
}

/// <summary>Stock.Insufficient (409): the document would take more than is in stock. Nothing was saved.</summary>
public sealed record InsufficientStockError(IReadOnlyList<StockShortageResponse> Shortages)
    : Error(ErrorCode, "Not enough stock.", ErrorType.Conflict)
{
    public const string ErrorCode = "Stock.Insufficient";
}
