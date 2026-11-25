namespace Erp.Modules.Warehouse.Contracts;

/// <summary>Permissions the Warehouse module checks. Companies combine them into roles.</summary>
public static class WarehousePermissions
{
    public const string WarehousesRead = "warehouse.warehouses.read";
    public const string WarehousesWrite = "warehouse.warehouses.write";
    public const string StockDocumentsRead = "warehouse.stock-documents.read";

    /// <summary>Create, edit and delete drafts.</summary>
    public const string StockDocumentsWrite = "warehouse.stock-documents.write";

    /// <summary>Confirm drafts and storno confirmed documents: the steps that move stock.</summary>
    public const string StockDocumentsConfirm = "warehouse.stock-documents.confirm";

    public const string StockRead = "warehouse.stock.read";

    /// <summary>Warehouse configuration, e.g. document number series.</summary>
    public const string SettingsWrite = "warehouse.settings.write";

    public static IReadOnlyList<string> All { get; } =
    [
        WarehousesRead,
        WarehousesWrite,
        StockDocumentsRead,
        StockDocumentsWrite,
        StockDocumentsConfirm,
        StockRead,
        SettingsWrite,
    ];
}
